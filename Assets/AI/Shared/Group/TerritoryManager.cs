using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [群体] 领地注册表（静态）：负责领地请求的注册、统一分配与查询。
/// 分配算法（贪心 + 距离场近似）：
///   在 NavGrid2D 可通行格上，为每个请求选"得分最高"的格子作为领地中心，
///   得分 = 到威胁源（玩家）的距离 + 到已分配领地中心的最小距离 × 权重。
///   即"远离威胁 + 远离其他领地"的最可通行区域，天然实现均分。
/// 防重叠（硬约束）：候选格与已分配领地中心的间距必须 ≥ (半径和 × 安全系数)，
///   否则直接排除；全部被排除时回退到软约束候选（取分数最高格，尽量远离）。
/// 威胁源动态化（低频、不冒犯）：玩家位移超过阈值时，以"当前领地中心"为基准
///   重新分配，领地只会在原位置附近微调，不频繁抢位/漂移；由 RefreshForThreat
///   节流驱动（各 BT Update 调用，内部几乎零开销）。
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
    }

    private static readonly List<Request> _pending = new List<Request>();
    private static readonly Dictionary<string, Territory> _assigned = new Dictionary<string, Territory>();
    private static readonly List<Vector2> _assignedCenters = new List<Vector2>();

    private static string _sceneKey;           // 当前场景名：检测场景切换以清理旧数据
    private static Vector2? _lastThreatPos;    // 上次分配时记录的威胁源（玩家）位置
    private static float _lastThreatCheckTime; // 上次威胁源检查时间（节流）

    // ---- 分配参数（默认值，可由具体 BT 覆盖）----
    private static float _individualRadius = 6f;   // 个体领地半径
    private static float _sharedRadius = 15f;      // 共享群领地半径
    private static float _searchRadius = 20f;      // 基准点周围的领地中心搜索半径
    private static float _occupiedWeight = 1f;     // 已占用距离权重（相对威胁距离）
    private static float _minSeparationFactor = 1.1f;    // 硬约束安全系数：中心距 ≥ (半径和 × 此系数)
    private static float _threatReassignDistance = 10f;  // 玩家位移超过该值（米）才触发重分配（不冒犯）
    private static float _threatRefreshInterval = 1.5f;  // 威胁源检查节流（秒）

    /// <summary>
    /// 注册一个领地请求。同 OwnerKey 去重（个体/共享群的多个成员共享同一领地）。
    /// </summary>
    /// <param name="ownerKey">拥有者键：个体=实例唯一键，共享群=群 ID</param>
    /// <param name="spawnPos">出生点（领地中心搜索的基准）</param>
    /// <param name="regionType">区域类型约束（Generic=不限定）</param>
    /// <param name="isShared">是否共享领地（鱼群等）；共享半径更大</param>
    public static void Register(string ownerKey, Vector2 spawnPos,
        AnimalRegion.RegionType regionType, bool isShared)
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
            IsShared = isShared
        });
    }

    /// <summary>
    /// 统一分配所有待分配的领地。无待分配或网格不可用时直接返回（不报错，由调用方兜底）。
    /// 请求列表保留（供动态重分配复用），调用方以首帧标志保证只执行一次。
    /// </summary>
    public static void EnsureAssigned()
    {
        if (_pending.Count == 0) return;

        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null) return;
        grid.EnsureBaked();

        _lastThreatPos = FindThreat();
        ReassignAll(grid, _lastThreatPos);
    }

    /// <summary>
    /// 低频威胁源动态化：节流检查玩家位置，位移超过阈值时以当前领地中心为基准重新分配。
    /// 设计为"不冒犯"：只在新威胁出现且玩家明显移动后才触发；重分配基准=当前中心，
    /// 领地只在原位置附近微调，不会频繁抢位或漂移。无玩家/无网格/无请求时直接返回。
    /// </summary>
    public static void RefreshForThreat()
    {
        if (Time.time < _lastThreatCheckTime) return;
        _lastThreatCheckTime = Time.time + _threatRefreshInterval;

        if (_pending.Count == 0) return;

        Vector2? threatPos = FindThreat();
        if (!threatPos.HasValue) return;
        if (_lastThreatPos.HasValue &&
            Vector2.Distance(threatPos.Value, _lastThreatPos.Value) < _threatReassignDistance)
            return;

        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null) return;
        grid.EnsureBaked();

        _lastThreatPos = threatPos;
        ReassignAll(grid, threatPos.Value);
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
        _lastThreatPos = null;
        _lastThreatCheckTime = 0f;
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
    /// 逐个（重新）分配所有请求。
    /// 首次分配基准 = 出生点；动态重分配基准 = 当前领地中心（尽量不漂移，实现"不冒犯"）。
    /// </summary>
    private static void ReassignAll(NavGrid2D grid, Vector2? threatPos)
    {
        Dictionary<string, Territory> next = new Dictionary<string, Territory>();
        List<Vector2> centers = new List<Vector2>();

        foreach (Request req in _pending)
        {
            Vector2 basePos = _assigned.TryGetValue(req.OwnerKey, out Territory old)
                ? old.Center   // 动态重分配：以当前领地中心为基准
                : req.SpawnPos; // 首次分配：以出生点为基准
            Territory t = Assign(grid, req, basePos, threatPos);
            next[req.OwnerKey] = t;
            centers.Add(t.Center);
        }

        _assigned.Clear();
        foreach (KeyValuePair<string, Territory> kv in next)
            _assigned[kv.Key] = kv.Value;
        _assignedCenters.Clear();
        _assignedCenters.AddRange(centers);
    }

    /// <summary>
    /// 为单个请求贪心选领地中心：遍历网格可通行格，取"威胁距离 + 已占用距离加权"最高者。
    /// 硬约束：与已分配领地中心距必须 ≥ (半径和 × 安全系数)，否则排除；
    ///   全部被排除时回退软约束候选（分数最高格，最大化间距）。
    /// 无可用格/无威胁源时兜底返回基准点。
    /// </summary>
    private static Territory Assign(NavGrid2D grid, Request req, Vector2 basePos, Vector2? threatPos)
    {
        float radius = req.IsShared ? _sharedRadius : _individualRadius;

        Vector2 best = basePos;                // 硬约束下最佳候选
        Vector2 fallback = basePos;            // 软约束兜底候选
        float bestScore = float.NegativeInfinity;
        float fallbackScore = float.NegativeInfinity;

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

                // 软约束分数：远离威胁源 + 远离其他领地（不含自己）
                float score = 0f;
                if (threatPos.HasValue)
                    score += Vector2.Distance(world, threatPos.Value);

                float minOccupied = float.MaxValue;
                foreach (Territory other in _assigned.Values)
                {
                    if (other.OwnerKey == req.OwnerKey) continue;
                    float d = Vector2.Distance(world, other.Center);
                    if (d < minOccupied) minOccupied = d;
                }
                if (minOccupied < float.MaxValue)
                    score += minOccupied * _occupiedWeight;

                if (score > fallbackScore)
                {
                    fallbackScore = score;
                    fallback = world;
                }

                // 硬约束：与已分配领地（不含自己）的中心距必须 ≥ (半径和 × 安全系数)
                bool conflict = false;
                foreach (Territory other in _assigned.Values)
                {
                    if (other.OwnerKey == req.OwnerKey) continue;
                    float minDist = (radius + other.Radius) * _minSeparationFactor;
                    if (Vector2.Distance(world, other.Center) < minDist)
                    {
                        conflict = true;
                        break;
                    }
                }
                if (conflict) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = world;
                }
            }
        }

        // 硬约束下无可用格 → 回退软约束候选（最大化间距，尽量不重叠）
        Vector2 center = bestScore > float.NegativeInfinity ? best : fallback;
        return new Territory { OwnerKey = req.OwnerKey, Center = center, Radius = radius, IsShared = req.IsShared };
    }

    /// <summary>查找威胁源（玩家）位置；未找到返回 null。</summary>
    private static Vector2? FindThreat()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? (Vector2?)player.transform.position : null;
    }
}
