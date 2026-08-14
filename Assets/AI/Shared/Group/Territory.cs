using UnityEngine;

/// <summary>
/// [群体] 领地：动物"家"的活动范围。
/// 拥有者（OwnerKey）：个体动物 = 唯一键（实例），共享领地 = 群 ID（如鱼群 FlockId）。
/// 中心由 TerritoryManager 在"远离威胁源 + 可通行 + 远离其他领地"的网格上选出。
/// 用途：
///   - 安全点/漫游/觅食的基准中心（替代出生点）
///   - A* 代价：领地外高代价（但低于玩家威胁代价），让动物倾向待在领地内。
/// </summary>
public class Territory
{
    /// <summary>拥有者键（个体实例 ID / 共享群 ID）。</summary>
    public string OwnerKey;

    /// <summary>领地中心（世界坐标）。</summary>
    public Vector2 Center;

    /// <summary>领地半径（米）：个体小、共享群大。</summary>
    public float Radius;

    /// <summary>是否共享领地（鱼群等）：供可视化/调试区分颜色。</summary>
    public bool IsShared;

    /// <summary>世界坐标是否落在领地内。</summary>
    public bool Contains(Vector2 worldPos) => Vector2.Distance(worldPos, Center) <= Radius;
}
