using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* 寻路网格：场景挂一个，Start 时按所有 AnimalRegion 的包围盒烘焙网格，
/// 并用 Physics2D 采样物理障碍（Tilemap 地面等）生成 blocked 数组。
/// 危险物（_hazardTags 中配置的 Tag，如 Spike）所在格也会被标记为不可通行，
/// 使领地分配/A* 寻路自动避开，新增危险物只需在 Inspector 配置 Tag。
/// 配置 _groundLayers 后还会做悬空检测：无地面支撑的格子视为不可通行。
/// 网格只存"是否物理可通行"；区域外代价 / 玩家高代价等运行时动态代价
/// 由调用方在 FindPath 时通过 costAt 委托传入，网格保持通用。
/// 可供鱼（水域）、蜘蛛（蜘蛛网）等共用同一个网格实例。
/// </summary>
public class NavGrid2D : MonoBehaviour
{
    public static NavGrid2D Instance { get; private set; }

    [Tooltip("网格单元大小（米）。越小越精细、烘焙/搜索越慢")]
    [SerializeField] private float _cellSize = 0.5f;

    [Tooltip("物理障碍层：这些层上的碰撞体所在格标记为不可通行（Tilemap 地面/岩石等）")]
    [SerializeField] private LayerMask _obstacleLayers;

    [Tooltip("危险物 Tag 列表：命中这些 Tag 的碰撞体所在格一律不可通行（无论层配置）。\n如尖刺 Spike，可配置扩展后续危险物，不写死在代码")]
    [SerializeField] private string[] _hazardTags = { "Spike" };

    [Tooltip("危险物扫描层：默认 All=全层扫描，配合 Tag 过滤（烘焙期一次扫描，开销可忽略）")]
    [SerializeField] private LayerMask _hazardLayers = ~0;

    [Tooltip("地面支撑层：用于悬空检测。配置后，格中心下方无地面支撑（Raycast 未命中）的格子标记为不可通行，\n防止领地/A* 落在悬空格（如平台边缘外的空气区）")]
    [SerializeField] private LayerMask _groundLayers;

    [Tooltip("是否启用悬空检测（需 _groundLayers 非空才生效）")]
    [SerializeField] private bool _enableGroundSupportCheck = true;

    [Tooltip("格中心到障碍物的间隙：采样盒 = 格大小 × 此系数，略小于格避免贴边格误判")]
    [SerializeField] private float _clearanceFactor = 0.8f;

    [Tooltip("网格边界的额外扩边（米），防止区域边界处目标点无法落格")]
    [SerializeField] private float _boundsMargin = 1f;

    private bool[,] _blocked;   // true = 物理不可通行
    private Vector2 _origin;    // 网格左下角世界坐标
    private int _width;
    private int _height;
    private Vector2 _worldSize;

    public float CellSize => _cellSize;
    public Vector2 Origin => _origin;
    public int Width => _width;
    public int Height => _height;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{name}: 场景已有 NavGrid2D，忽略重复实例", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Bake();
    }

    /// <summary>
    /// 惰性烘焙：网格尚未烘焙时立即执行 Bake。
    /// 解决"鱼的 Awake 生成安全点早于网格 Start 烘焙"的时序问题：
    /// Awake 期间 _blocked 为 null，直接访问会得到空结果。
    /// </summary>
    public void EnsureBaked()
    {
        if (_blocked == null)
            Bake();
    }

    /// <summary>
    /// 烘焙网格：合并所有 AnimalRegion 的包围盒作为边界，逐格用 Physics2D 采样障碍。
    /// </summary>
    public void Bake()
    {
        Bounds bounds = ComputeBounds();
        _worldSize = new Vector2(bounds.size.x, bounds.size.y);
        _width = Mathf.Max(1, Mathf.CeilToInt(_worldSize.x / _cellSize));
        _height = Mathf.Max(1, Mathf.CeilToInt(_worldSize.y / _cellSize));
        _origin = new Vector2(bounds.min.x, bounds.min.y);

        _blocked = new bool[_width, _height];

        // 采样盒尺寸略小于格，避免相邻障碍格边缘误判
        Vector2 sampleSize = Vector2.one * (_cellSize * _clearanceFactor);

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                Vector2 center = CellToWorld(x, y);
                if (_obstacleLayers.value != 0)
                {
                    Collider2D hit = Physics2D.OverlapBox(center, sampleSize, 0f, _obstacleLayers);
                    _blocked[x, y] = hit != null && !hit.isTrigger;
                }

                // 危险物识别：命中 _hazardTags 中任意 Tag 的碰撞体所在格一律不可通行
                // （不写死具体危险物，新增危险物只需在 Inspector 配置 Tag）
                if (!_blocked[x, y] && _hazardTags.Length > 0)
                {
                    Collider2D hazardHit = Physics2D.OverlapBox(center, sampleSize, 0f, _hazardLayers);
                    if (hazardHit != null)
                    {
                        for (int t = 0; t < _hazardTags.Length; t++)
                        {
                            if (hazardHit.CompareTag(_hazardTags[t]))
                            {
                                _blocked[x, y] = true;
                                break;
                            }
                        }
                    }
                }

                // 悬空检测：格中心下方无地面支撑则视为不可通行（防止领地/寻路落在悬空格）
                if (!_blocked[x, y] && _enableGroundSupportCheck && _groundLayers.value != 0)
                {
                    RaycastHit2D groundHit = Physics2D.Raycast(center, Vector2.down, _cellSize * 2f, _groundLayers);
                    if (groundHit.collider == null || groundHit.collider.isTrigger)
                        _blocked[x, y] = true;
                }
            }
        }

        Debug.Log($"[NavGrid2D] 烘焙完成：{_width}×{_height} 格，格大小 {_cellSize}m，边界 {_worldSize}m", this);
    }

    /// <summary>
    /// 计算网格边界：所有 AnimalRegion 包围盒合并 + 扩边。
    /// 无区域时退化为全场景物体范围（兜底）。
    /// </summary>
    private Bounds ComputeBounds()
    {
        Bounds result = new Bounds();
        bool hasAny = false;

        var regions = AnimalRegionRegistry.All();
        foreach (AnimalRegion region in regions)
        {
            Bounds b = region.Bounds;
            if (!hasAny) { result = b; hasAny = true; }
            else result.Encapsulate(b);
        }

        if (!hasAny)
        {
            // 兜底：以当前物体为 100×100 的方形
            result = new Bounds(transform.position, Vector2.one * 100f);
        }

        result.Expand(_boundsMargin);
        return result;
    }

    /// <summary>世界坐标 → 网格索引。越界返回 -1。</summary>
    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        EnsureBaked();
        int x = Mathf.FloorToInt((worldPos.x - _origin.x) / _cellSize);
        int y = Mathf.FloorToInt((worldPos.y - _origin.y) / _cellSize);
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            return new Vector2Int(-1, -1);
        return new Vector2Int(x, y);
    }

    /// <summary>网格索引 → 格中心世界坐标。</summary>
    public Vector2 CellToWorld(int x, int y)
    {
        EnsureBaked();
        return _origin + new Vector2((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize);
    }

    /// <summary>该格是否物理不可通行。</summary>
    public bool IsBlocked(int x, int y)
    {
        EnsureBaked();
        if (x < 0 || y < 0 || x >= _width || y >= _height)
            return true;
        return _blocked[x, y];
    }

    public bool IsBlocked(Vector2Int cell) => IsBlocked(cell.x, cell.y);
}
