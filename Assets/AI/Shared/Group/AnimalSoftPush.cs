using UnityEngine;

/// <summary>
/// [群体] MC 式软推开：同类/同群动物重叠时沿最短穿透轴把"自己"推开（双方各承担一半）。
/// 与 Boids 分离（行为层方向修正）互补：本组件是物理层位移，兜底解决"扎堆穿模/挤死"。
/// 规则（对应设计约定）：
/// ① 只推同群（FlockId 相同，无 FlockMember 时回落同 Tag）；查询层默认自身所在层，
///    玩家层永不包含，且推开只改"自己"的位置 → 玩家绝不被推；
/// ② FixedUpdate + OverlapCircleNonAlloc（遵循非分配规范），穿透向量直接改 Rb.position；
/// ③ 不调用 NotifyMoveCommand：推开是物理位移，不计入"移动指令"，防卡死天然规避。
/// 推开维度按物种配置：横板动物（蛙/羊）仅水平；蜘蛛（8向爬墙）/鱼（全向游泳）2D 全向。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AnimalSoftPush : MonoBehaviour
{
    /// <summary>推开维度：仅水平（横板标准）或 2D 全向（爬墙/游泳）。</summary>
    public enum PushDimension
    {
        HorizontalOnly,
        Omnidirectional
    }

    [Header("推开配置")]
    [Tooltip("推开维度：HorizontalOnly=仅水平（蛙/羊等横板动物）；Omnidirectional=2D全向（蜘蛛爬墙/鱼游泳）")]
    [SerializeField] private PushDimension _dimension = PushDimension.HorizontalOnly;

    [Tooltip("身体半径（米）：0=自动从首个非触发器碰撞体的 bounds 估算")]
    [SerializeField] private float _bodyRadius;

    [Tooltip("邻居查询半径（米）：只需覆盖'可能重叠'的范围，过大增加查询成本")]
    [SerializeField] private float _queryRadius = 1.0f;

    [Tooltip("每物理帧最大推开量（米）：限制推开速度避免瞬移感。0.08 ≈ 50Hz 下 4m/s 分离速度上限")]
    [SerializeField] private float _maxPushPerStep = 0.08f;

    [Tooltip("参与推开的层：0=自身所在层（同类动物）。玩家层绝不应包含在内")]
    [SerializeField] private LayerMask _pushLayers;

    private Rigidbody2D _rb;
    private FlockMember _flockMember;
    private float _radius;

    // GC 优化：NonAlloc 预分配缓冲（复用，同 EnvironmentMonitor 规范）
    private readonly Collider2D[] _hits = new Collider2D[8];

    /// <summary>推开维度（按物种由行为树配置）。</summary>
    public PushDimension Dimension { get => _dimension; set => _dimension = value; }

    /// <summary>身体半径（估算或配置值），供对方计算最小间距。</summary>
    public float BodyRadius => _radius;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _flockMember = GetComponent<FlockMember>();
        if (_pushLayers.value == 0)
            _pushLayers = 1 << gameObject.layer; // 默认只查同层动物（玩家层天然排除）
        _radius = _bodyRadius > 0f ? _bodyRadius : EstimateRadius();
    }

    /// <summary>从首个非触发器碰撞体估算身体半径（取较大半边，保证推开距离 ≥ 体宽）。</summary>
    private float EstimateRadius()
    {
        Collider2D[] cols = GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i].isTrigger) continue;
            Vector3 e = cols[i].bounds.extents;
            return Mathf.Max(e.x, e.y);
        }
        return 0.4f; // 无碰撞体兜底
    }

    private void FixedUpdate()
    {
        Vector2 selfPos = _rb.position;
        int count = Physics2D.OverlapCircleNonAlloc(selfPos, _queryRadius, _hits, _pushLayers);
        if (count == 0)
            return;

        string myFlock = _flockMember != null ? _flockMember.FlockId : gameObject.tag;
        Vector2 totalPush = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _hits[i];
            if (hit == null || hit.isTrigger) continue;             // 跳过感知用 trigger（如青蛙的 Box）
            if (hit.attachedRigidbody == _rb) continue;             // 跳过自身多碰撞体

            AnimalSoftPush other = hit.GetComponentInParent<AnimalSoftPush>();
            if (other == null || other == this) continue;

            // 只推同群/同类：FlockId 相同（无 FlockMember 时回落 Tag 比较）
            string otherFlock = other._flockMember != null ? other._flockMember.FlockId : other.gameObject.tag;
            if (otherFlock != myFlock) continue;

            Vector2 otherPos = other._rb != null ? other._rb.position : (Vector2)other.transform.position;
            Vector2 diff = selfPos - otherPos;
            float dist = diff.magnitude;
            float minDist = _radius + other._radius;
            if (dist >= minDist) continue;                          // 未重叠

            // 最短穿透轴：圆-圆近似下 diff 方向即最短分离方向
            float penetration = minDist - dist;
            Vector2 pushDir = ResolvePushDirection(diff, dist, other);

            // 双方各承担一半穿透量；限幅避免瞬移感
            float step = Mathf.Min(penetration * 0.5f, _maxPushPerStep);
            totalPush += pushDir * step;
        }

        // 直接改 Rb.position（物理位移），不调用 NotifyMoveCommand → 防卡死不误判
        if (totalPush.sqrMagnitude > 0f)
            _rb.position += totalPush;
    }

    /// <summary>
    /// 计算推开方向：沿穿透轴，按维度裁剪。
    /// 仅水平模式：近竖直重叠（|dx|≈0）时用实例 ID 大小给出确定性反向——
    /// 该比较对双方反对称（A>B 与 B>A 必反向），避免双方同向推导致不分离。
    /// </summary>
    private Vector2 ResolvePushDirection(Vector2 diff, float dist, AnimalSoftPush other)
    {
        float idSign = GetInstanceID() > other.GetInstanceID() ? 1f : -1f;

        if (_dimension == PushDimension.HorizontalOnly)
        {
            float sign = Mathf.Abs(diff.x) > 0.01f ? Mathf.Sign(diff.x) : idSign;
            return new Vector2(sign, 0f);
        }

        // 全向：完全重叠（dist≈0）时用 ID 反向兜底
        return dist > 1e-4f ? diff / dist : new Vector2(idSign, 0f);
    }
}
