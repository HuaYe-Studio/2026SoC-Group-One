using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [群体] 领地注册表（静态）：负责领地请求的注册、统一分配与查询。
/// 分配算法（贪心 + 距离场近似）：
///   在 NavGrid2D 可通行格上，为每个请求选"得分最高"的格子作为领地中心。
///   得分 = 到已分配领地中心的距离惩罚（小于安全间距则强惩罚），只考虑同类间分散。
///   领地只与出生点/同类分布有关，不受玩家等可移动物体影响。
/// 防重叠（梯度惩罚）：与其他领地的水平间距越近惩罚越大，分配时尽量分散；
///   若实在拥挤则取"离其他领地最远"的格，仍最大化间距、尽量不重叠。
/// 形状：水平椭圆（X 半径 > Y 半径），更贴合 2D 横板长条平台。
/// 场景切换：Register 检测到场景变化时自动 Reset，避免旧领地数据残留导致
///   重新进入场景后 key 去重跳过注册。
/// 时序：动物在 Awake 注册请求；所有 Awake 跑完后（Unity 保证早于首个 Update），
///   由任一动物的 BT 在首帧 Update 调用 EnsureAssigned() 统一分配。
/// </summary>
public static class TerritoryManager
{
    private class Request
    {
        public string OwnerKey;
        public Vector2 SpawnPos;
        public AnimalRegion.RegionType RegionType;
        public bool IsShared;
        public float Strength; // 个体强度分 0~1：决定个体领地半径
    }

    private static readonly List<Request> _pending = new List<Request>();
    private static readonly Dictionary<string, Territory> _assigned = new Dictionary<string, Territory>();
    private static readonly List<Vector2> _assignedCenters = new List<Vector2>();

    private static string _sceneKey;           // 当前场景名：检测场景切换以清理旧数据

    // ---- 分配参数（默认值，可由具体 BT 覆盖）----
    // 个体椭圆半径：X 沿地形（宽），Y 沿高度（窄，避免覆盖平台上方/下方）
    private static float _individualRadiusXMin = 3.5f;  // 个体领地下半宽最小（最弱个体）
    private static float _individualRadiusXMax = 7f;    // 个体领地下半宽最大（最强个体）
    private static float _individualRadiusYMin = 1.5f;  // 个体领地高度最小（最弱个体）
    private static float _individualRadiusYMax = 3f;    // 个体领地高度最大（最强个体）
    private static float _sharedRadiusX = 15f;          // 共享群领地半宽（沿地形）
    private static float _sharedRadiusY = 4f;           // 共享群领地高度（沿垂直）
    private static float _searchRadius = 12f;          // 基准点周围的领地中心搜索半径（12m：既不过远也够分散）
    private static float _occupiedWeight = 1f;     // 已占用距离权重（同类间分散程度）
    private static float _minSeparationFactor = 1.1f;    // 安全间距系数：水平中心距 ≥ (半径X和 × 此系数) 视为不冲突
    private static float _conflictPenalty = 30f;   // 冲突惩罚系数：越近惩罚越大，保证尽量分散（30=宁可远挤也不重叠）

    /// <summary>
    /// 注册一个领地请求。同 OwnerKey 去重（个体/共享群的多个成员共享同一领地）。
    /// </summary>
    /// <param name="ownerKey">拥有者键：个体=实例唯一键，共享群=群 ID</param>
    /// <param name="spawnPos">出生点（领地中心搜索的基准）</param>
    /// <param name="regionType">区域类型约束（Generic=不限定）</param>
    /// <param name="isShared">是否共享领地（鱼群等）；共享半径更大</param>
    /// <param name="strength">个体强度分 0~1（默认 1）：个体领地半径在 [min,max] 间按强度映射；共享领地忽略</param>
    public static void Register(string ownerKey, Vector2 spawnPos,
        AnimalRegion.RegionType regionType, bool isShared, float strength = 1f)
    {
        if (string.IsNullOrEmpty(ownerKey)) return;

        EnsureSceneReset();

        if (_assigned.ContainsKey(ownerKey)) return;
        foreach (Request r in _pending)
            if (r.OwnerKey == ownerKey) return;

        _pending.Add(new Request
        {
            OwnerKey = ownerKey,
            SpawnPos = spawnPos,
            RegionType = regionType,
            IsShared = isShared,
            Strength = Mathf.Clamp01(strength)
        });
    }

    /// <summary>
    /// 统一分配所有待分配的领地。无待分配或网格不可用时直接返回（不报错，由调用方兜底）。
    /// 请求列表保留（供场景重分配复用），调用方以首帧标志保证只执行一次。
    /// </summary>
    public static void EnsureAssigned()
    {
        if (_pending.Count == 0) return;

        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null) return;
        grid.EnsureBaked();

        ReassignAll(grid);
    }

    /// <summary>获取已分配的领地；未分配返回 null（调用方应回退到出生点）。</summary>
    public static Territory Get(string ownerKey)
    {
        if (string.IsNullOrEmpty(ownerKey)) return null;
        return _assigned.TryGetValue(ownerKey, out Territory t) ? t : null;
    }

    /// <summary>世界坐标是否落在指定拥有者的领地内（未分配返回 false）。</summary>
    public static bool Contains(string ownerKey, Vector2 worldPos)
    {
        Territory t = Get(ownerKey);
        return t != null && t.Contains(worldPos);
    }

    /// <summary>所有已分配的领地（只读遍历，供可视化/调试用）。</summary>
    public static IEnumerable<Territory> All()
    {
        return _assigned.Values;
    }

    /// <summary>清空所有注册与分配状态（场景切换/重开时调用）。</summary>
    public static void Reset()
    {
        _pending.Clear();
        _assigned.Clear();
        _assignedCenters.Clear();
    }

    /// <summary>
    /// 检测场景切换：场景变化时清理旧状态，避免旧领地数据残留导致重新注册被跳过。
    /// </summary>
    private static void EnsureSceneReset()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == _sceneKey) return;
        _sceneKey = scene;
        Reset();
    }

    /// <summary>
    /// 逐个分配所有请求。基准 = 出生点（领地与出生点/同类分布相关，不受玩家影响）。
    /// 注意：本轮已分配的领地会立即进入 _assigned，供后续请求"看到"并避开，
    ///   否则所有请求都会基于空列表打分、落到同一个高分格（重合 bug 的根因）。
    /// </summary>
    private static void ReassignAll(NavGrid2D grid)
    {
        // 清空旧分配：本轮边分配边写入，让后续请求能看到前面已分配的领地
        _assigned.Clear();
        _assignedCenters.Clear();

        foreach (Request req in _pending)
        {
            Territory t = Assign(grid, req, req.SpawnPos);
            _assigned[req.OwnerKey] = t;      // 立即可见，后续请求据此避开
            _assignedCenters.Add(t.Center);
        }
    }

    /// <summary>
    /// 为单个请求贪心选领地中心：遍历网格可通行格，取"与其他领地的水平间距惩罚"最小者。
    /// 惩罚是梯度（不是硬排除）：间距 < 安全间距时按"重叠量"强惩罚，间距 ≥ 安全间距不惩罚；
    ///   从而保证每个请求都尽量分散，即使拥挤也取"离其他领地最远"的格。
    /// 无可用格时兜底返回基准点。
    /// </summary>
    private static Territory Assign(NavGrid2D grid, Request req, Vector2 basePos)
    {
        // 椭圆半径随强度分映射（X 宽、Y 窄）；共享群固定
        float radiusX, radiusY;
        if (req.IsShared)
        {
            radiusX = _sharedRadiusX;
            radiusY = _sharedRadiusY;
        }
        else
        {
            radiusX = Mathf.Lerp(_individualRadiusXMin, _individualRadiusXMax, req.Strength);
            radiusY = Mathf.Lerp(_individualRadiusYMin, _individualRadiusYMax, req.Strength);
        }

        Vector2 best = basePos;                // 惩罚最小（最分散）的候选
        float bestScore = float.NegativeInfinity;

        // 个体差异化偏移：基于实例 ID 的哈希，给每个请求一个固定的水平偏移，
        // 让同分候选格产生"个体倾向"，避免两只青蛙分到同一个高分格导致行为完全一致
        int instanceHash = req.OwnerKey.GetHashCode();
        float instanceBiasX = ((instanceHash % 100) / 100f - 0.5f) * _individualRadiusXMax * 0.3f; // ±30% 半径偏移

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (grid.IsBlocked(x, y)) continue;

                Vector2 world = grid.CellToWorld(x, y);
                if (Vector2.Distance(world, basePos) > _searchRadius) continue;

                // 区域约束：指定类型时要求落在该类区域内
                if (req.RegionType != AnimalRegion.RegionType.Generic &&
                    !AnimalRegionRegistry.Contains(world, req.RegionType))
                    continue;

                // 分数 = 与最近其他领地的水平间距惩罚（梯度，不是硬排除）
                // 间距 < 安全间距时按重叠量强惩罚；间距 ≥ 安全间距不惩罚（0 分）。
                float minDistX = float.MaxValue;   // 与最近其他领地的水平间距
                float minNeededX = 0f;             // 对应的所需安全间距
                foreach (Territory other in _assigned.Values)
                {
                    if (other.OwnerKey == req.OwnerKey) continue;
                    float dx = Mathf.Abs(world.x - other.Center.x);
                    if (dx < minDistX)
                    {
                        minDistX = dx;
                        minNeededX = (radiusX + other.RadiusX) * _minSeparationFactor;
                    }
                }

                float score;
                if (minDistX < float.MaxValue && minDistX < minNeededX)
                {
                    // 冲突：水平间距不足，按重叠量强惩罚（越近分越低）
                    score = (minDistX - minNeededX) * _conflictPenalty;
                }
                else
                {
                    // 无冲突：间距充足，不惩罚（同分下优先取离其他领地更远的格）
                    score = minDistX < float.MaxValue ? minDistX * _occupiedWeight : 0f;
                }

                // 个体差异化微调：让同分候选格产生"个体倾向"，避免行为完全一致
                score += Mathf.Abs(world.x - basePos.x - instanceBiasX) * 0.1f; // 惩罚偏离个体偏好位置的格

                if (score > bestScore)
                {
                    bestScore = score;
                    best = world;
                }
            }
        }

        return new Territory { OwnerKey = req.OwnerKey, Center = best, RadiusX = radiusX, RadiusY = radiusY, IsShared = req.IsShared };
    }
}
