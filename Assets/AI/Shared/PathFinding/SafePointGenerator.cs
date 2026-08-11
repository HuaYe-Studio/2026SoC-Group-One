using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 安全点系统：为动物生成多个安全点（避免单点导致来回抖动），并提供带迟滞的选择逻辑。
/// - 生成：以出生点为中心，在指定类型区域内随机采样 N 个物理可通行的点（A* 网格校验）
/// - 选择：当前点安全（离玩家远/玩家不可见）则保持（迟滞），否则切到最近的安全点
/// 安全点数量 > 1 是防抖关键：单点 + 玩家高代价仍可能出现"去→被赶→回→去"的逻辑循环。
/// </summary>
public static class SafePointGenerator
{
    /// <summary>
    /// 生成 N 个安全点。
    /// </summary>
    /// <param name="center">基准中心（如出生点）</param>
    /// <param name="count">安全点数量（建议 3~5）</param>
    /// <param name="radius">采样半径（米）</param>
    /// <param name="regionType">限定区域类型（Generic=不限定，Water=水域，SpiderWeb=蜘蛛网）</param>
    /// <param name="minSpacing">点间最小间距（米），保证分散</param>
    public static Vector2[] GenerateSafePoints(Vector2 center, int count, float radius,
        AnimalRegion.RegionType regionType, float minSpacing = 3f)
    {
        List<Vector2> candidates = CollectCandidates(center, radius, regionType);
        if (candidates.Count == 0)
            return new[] { center };

        // 从候选点随机采样，尽量分散（顺序打乱 + 最小间距过滤）
        Shuffle(candidates);

        List<Vector2> result = new List<Vector2>();
        foreach (Vector2 c in candidates)
        {
            if (result.Count >= count) break;

            bool tooClose = false;
            foreach (Vector2 picked in result)
            {
                if (Vector2.Distance(c, picked) < minSpacing) { tooClose = true; break; }
            }
            if (!tooClose)
                result.Add(c);
        }

        // 数量不足时补足（放宽间距）
        for (int i = 0; i < candidates.Count && result.Count < count; i++)
        {
            bool dup = false;
            foreach (Vector2 picked in result)
            {
                if (Vector2.Distance(candidates[i], picked) < 0.5f) { dup = true; break; }
            }
            if (!dup)
                result.Add(candidates[i]);
        }

        return result.ToArray();
    }

    /// <summary>
    /// 带迟滞的安全点选择：当前点安全则保持，否则选最近的安全点。
    /// </summary>
    /// <param name="safePoints">安全点列表</param>
    /// <param name="currentIndex">当前选中索引（引用，便于迟滞保持）</param>
    /// <param name="position">动物当前位置</param>
    /// <param name="playerPos">玩家位置（仅 playerVisible 时有意义）</param>
    /// <param name="playerVisible">玩家是否可见</param>
    /// <param name="safeRadius">安全判定半径：与玩家距离 ≥ 此值视为安全</param>
    /// <param name="switchHysteresis">切换迟滞：接近当前点（此距离内）不切换</param>
    public static Vector2 SelectSafePoint(Vector2[] safePoints, ref int currentIndex,
        Vector2 position, Vector2 playerPos, bool playerVisible,
        float safeRadius, float switchHysteresis = 1f)
    {
        if (safePoints == null || safePoints.Length == 0)
            return position;

        currentIndex = Mathf.Clamp(currentIndex, 0, safePoints.Length - 1);
        Vector2 current = safePoints[currentIndex];

        // 迟滞：当前点仍安全 → 保持，防止来回切
        if (!playerVisible || Vector2.Distance(current, playerPos) >= safeRadius)
            return current;

        // 玩家可见且当前点不安全 → 找最近且安全（离玩家远）的点
        float bestScore = float.NegativeInfinity;
        int bestIndex = currentIndex;
        for (int i = 0; i < safePoints.Length; i++)
        {
            Vector2 p = safePoints[i];
            if (Vector2.Distance(p, playerPos) < safeRadius)
                continue; // 该点也在玩家附近，跳过
            // 分数 = 远离玩家程度 - 距离惩罚（优先远离玩家，其次就近）
            float score = Vector2.Distance(p, playerPos) - Vector2.Distance(position, p) * 0.5f;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        // 全部都不安全 → 切到离当前位置最近的点（尽力而为）
        if (bestScore == float.NegativeInfinity)
        {
            float nearestDist = float.MaxValue;
            for (int i = 0; i < safePoints.Length; i++)
            {
                float d = Vector2.Distance(position, safePoints[i]);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    bestIndex = i;
                }
            }
        }

        // 切换迟滞：目标点没比当前点明显更好时不切
        if (Vector2.Distance(position, current) < switchHysteresis &&
            safePoints[bestIndex] == current)
            return current;

        currentIndex = bestIndex;
        return safePoints[currentIndex];
    }

    /// <summary>收集区域内、物理可通行、且距离中心在半径内的候选点。</summary>
    private static List<Vector2> CollectCandidates(Vector2 center, float radius,
        AnimalRegion.RegionType regionType)
    {
        List<Vector2> result = new List<Vector2>();
        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null)
        {
            result.Add(center);
            return result;
        }

        // 惰性烘焙：鱼的 Awake 可能早于网格 Start，确保网格已烘焙再遍历
        grid.EnsureBaked();

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                if (grid.IsBlocked(x, y))
                    continue;

                Vector2 world = grid.CellToWorld(x, y);
                if (Vector2.Distance(world, center) > radius)
                    continue;

                // 区域限定：指定类型时要求落在该类区域内（统一走注册表查询）
                if (regionType != AnimalRegion.RegionType.Generic &&
                    !AnimalRegionRegistry.Contains(world, regionType))
                    continue;

                result.Add(world);
            }
        }
        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
