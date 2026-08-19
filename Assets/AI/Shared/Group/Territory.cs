using UnityEngine;

/// <summary>
/// [群体] 领地：动物"家"的活动范围。
/// 拥有者（OwnerKey）：个体动物 = 唯一键（实例），共享领地 = 群 ID（如鱼群 FlockId）。
/// 中心由 TerritoryManager 在"可通行 + 远离其他领地"的网格上选出。
/// 形状为水平椭圆（RadiusX 沿地形延展，RadiusY 沿高度收紧），更贴合 2D 横板的长条平台。
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

    /// <summary>椭圆水平半径（米）：沿 2D 横板地形延展，通常 > RadiusY。</summary>
    public float RadiusX;

    /// <summary>椭圆垂直半径（米）：沿高度方向收紧，避免覆盖平台上方/下方。</summary>
    public float RadiusY;

    /// <summary>兼容保留：水平半径（=RadiusX）。历史调用方/调试读取用。</summary>
    public float Radius => RadiusX;

    /// <summary>是否共享领地（鱼群等）：供可视化/调试区分颜色。</summary>
    public bool IsShared;

    /// <summary>世界坐标是否落在椭圆领地内（(dx/rx)² + (dy/ry)² ≤ 1）。</summary>
    public bool Contains(Vector2 worldPos)
    {
        float dx = worldPos.x - Center.x;
        float dy = worldPos.y - Center.y;
        return (dx * dx) / (RadiusX * RadiusX) + (dy * dy) / (RadiusY * RadiusY) <= 1f;
    }
}
