using UnityEngine;

/// <summary>
/// 区域注册表：统一 AnimalRegion 的发现与查询，避免各组件各自 FindObjectsByType 扫描。
/// 消费方（NavGrid2D 烘焙、SafePointGenerator 采样、BT 代价判定）直接查询本类。
/// 按帧缓存：同一帧内多次查询复用结果——A* 代价采样会逐格调用 Contains，避免重复扫描。
/// </summary>
public static class AnimalRegionRegistry
{
    private static AnimalRegion[] _cache;
    private static int _cacheFrame = -1;

    /// <summary>场景中所有 AnimalRegion（按帧缓存）。</summary>
    public static AnimalRegion[] All()
    {
        int frame = Time.frameCount;
        if (_cache != null && _cacheFrame == frame)
            return _cache;

        _cache = Object.FindObjectsByType<AnimalRegion>(FindObjectsSortMode.None);
        _cacheFrame = frame;
        return _cache;
    }

    /// <summary>世界坐标是否落在指定类型区域的内部。</summary>
    public static bool Contains(Vector2 worldPos, AnimalRegion.RegionType type)
    {
        foreach (AnimalRegion region in All())
        {
            if (region != null && region.Type == type && region.Contains(worldPos))
                return true;
        }
        return false;
    }
}
