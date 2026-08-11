using UnityEngine;

/// <summary>
/// 动物活动区域标记：场景中放置一个带 Collider2D（推荐 PolygonCollider2D）的 GameObject，
/// 用来划定某类动物的活动范围，供 A* 网格做"区域内低代价 / 区域外高代价"判定。
/// - 鱼 → Water（水域）：区域外视为空气，极高代价，鱼不会游上岸
/// - 蜘蛛 → SpiderWeb（蜘蛛网）：区域外高代价，蜘蛛尽可能留在网内
/// - Generic：通用区域，供需要限定活动范围的动物使用
/// </summary>
public class AnimalRegion : MonoBehaviour
{
    public enum RegionType { Water, SpiderWeb, Generic }

    [Tooltip("区域类型：决定该区域为哪些动物的通行低代价区")]
    [SerializeField] private RegionType _type = RegionType.Water;

    [Tooltip("区域外（空气）的通行代价：越高越难进入。0 = 不限制")]
    [SerializeField] private float _outsideCost = 1000f;

    private Collider2D _collider;

    public RegionType Type => _type;
    public float OutsideCost => _outsideCost;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider == null)
            Debug.LogWarning($"{name}: AnimalRegion 需要 Collider2D 定义区域形状（推荐 PolygonCollider2D）", this);
    }

    /// <summary>世界坐标点是否位于本区域内。</summary>
    public bool Contains(Vector2 worldPos)
    {
        if (_collider == null)
            return false;
        // OverlapPoint 仅对 Polygon/Box 等几何体可靠；这里通用处理
        return _collider.OverlapPoint(worldPos);
    }

    /// <summary>区域的世界包围盒（供网格烘焙计算 bounds 用）。</summary>
    public Bounds Bounds => _collider != null ? _collider.bounds : new Bounds(transform.position, Vector3.one);
}
