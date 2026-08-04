using UnityEngine;

/// <summary>
/// 环境监视器：感知层，负责检测周围环境并写入 Blackboard。
/// 感知与认知分层：检测层只做原始感知（视野扇形/听觉圆形），
/// 认知结果（威胁等级、记忆、语义状态）统一写入 Blackboard 供决策层读取。
/// </summary>
public class EnvironmentMonitor : MonoBehaviour
{
    [Header("Threat Perception (Vision Cone + Hearing Circle)")]
    [Tooltip("视觉距离：扇形视野的半径")]
    [SerializeField] private float _visionRadius = 8f;
    [Tooltip("视野半角（度），完整视野角为其两倍")]
    [SerializeField, Range(0f, 180f)] private float _visionHalfAngle = 60f;
    [Tooltip("听觉半径：全向圆形检测，无视方位")]
    [SerializeField] private float _hearingRadius = 4f;
    [SerializeField] private LayerMask _threatLayer;
    [Tooltip("逃跑触发半径：玩家进入此距离视为紧迫威胁")]
    [SerializeField] private float _fleeRadius = 5f;
    [Tooltip("威胁解除半径：玩家离开此距离才视为安全（需 > 触发半径，避免临界区抖动）")]
    [SerializeField] private float _safeRadius = 8f;

    [Header("Threat Cognition (Memory + Camouflage)")]
    [Tooltip("目标速度低于该值视为静止（伪装生效）")]
    [SerializeField] private float _moveSpeedThreshold = 0.5f;
    [Tooltip("看到移动目标时威胁值每秒上升量")]
    [SerializeField] private float _threatRiseRate = 40f;
    [Tooltip("目标静止时威胁值每秒下降量")]
    [SerializeField] private float _threatCalmRate = 10f;
    [Tooltip("失去感知后威胁值每秒衰减量")]
    [SerializeField] private float _threatDecayRate = 15f;
    [Tooltip("失去感知后记忆的保留时长（秒），超时彻底遗忘")]
    [SerializeField] private float _memoryDuration = 5f;

    [Header("Food Detection")]
    [SerializeField] private float _foodRadius = 10f;
    [SerializeField] private LayerMask _foodLayer;

    [Header("Terrain Detection")]
    [SerializeField] private float _groundCheckDistance = 1.5f;
    [SerializeField] private float _wallCheckDistance = 1f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Fellow Detection")]
    [SerializeField] private float _fellowRadius = 5f;
    [SerializeField] private LayerMask _fellowLayer;

    [Header("Debug")]
    [Tooltip("开启后：威胁值/玩家可见性/形态判定发生变化时输出日志")]
    [SerializeField] private bool _enableDebugLog;

    // 黑板引用：所有感知结果统一写入这里
    private Blackboard _bb;
    private SpriteRenderer _spriteRenderer;
    private PlayerController _playerController;
    private DevourableAnimal _devourable; // 提供自身形态，用于同形态友好判定

    // 调试用：记录上次输出时的状态，只在变化时输出
    private bool _lastLogVisible;
    private bool _lastLogSameForm;
    private int _lastLogThreatBucket = -1;

    // 保留给外部调试/兼容的瞬时感知属性（黑板未接入前可用）
    public float ThreatRadius => _visionRadius;
    public float FleeRadius => _fleeRadius;
    public float FoodRadius => _foodRadius;
    public float FellowRadius => _fellowRadius;

    // 同类信息（保留原列表，暂未接入黑板）
    public System.Collections.Generic.List<Transform> NearbyFellows { get; private set; }
        = new System.Collections.Generic.List<Transform>();
    public int FellowCount => NearbyFellows.Count;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _devourable = GetComponent<DevourableAnimal>();
        _bb = GetComponent<AnimalBase>()?.Board;
        if (_bb == null)
        {
            Debug.LogError($"{name}: EnvironmentMonitor 需要 AnimalBase 提供 Blackboard", this);
            enabled = false;
            return;
        }
        _bb.MemoryDuration = _memoryDuration;
        _bb.FleeRadius = _fleeRadius;
        _bb.SafeRadius = _safeRadius;

        if (_threatLayer.value == 0)
            Debug.LogWarning($"{name}: EnvironmentMonitor 的 Threat Layer 未配置（Nothing），玩家检测将永远不生效", this);
    }

    private void Update()
    {
        if (_bb == null) return;

        // 眩晕期间不感知：清空可见标记，防止眩晕中威胁值继续上涨、
        // 导致吐出后 AI 立即因贴脸玩家而受惊逃跑（B5）
        if (_bb.IsStunned)
        {
            _bb.IsPlayerVisible = false;
            return;
        }

        _bb.AnimalPosition = transform.position;

        DetectThreats();
        DetectFood();
        DetectTerrain();
        DetectFellows();

        if (_enableDebugLog)
            LogPerceptionChange();
    }

    /// <summary>
    /// 调试输出：威胁值（按10分桶）/玩家可见性/形态判定任一变化时打印一条日志。
    /// 用于验证威胁值系统与玩家形态检测是否正常工作。
    /// </summary>
    private void LogPerceptionChange()
    {
        int threatBucket = Mathf.FloorToInt(_bb.ThreatLevel / 10f) * 10;

        bool changed = _lastLogVisible != _bb.IsPlayerVisible
            || _lastLogSameForm != _bb.IsPlayerSameForm
            || _lastLogThreatBucket != threatBucket;
        if (!changed) return;

        FormType? resolvedForm = GetPlayerFormType();
        string playerForm = resolvedForm.HasValue ? resolvedForm.Value.ToString() : "未找到";
        string ownForm = _devourable != null ? _devourable.GrantedForm.ToString() : "无DevourableAnimal";

        Debug.Log($"{name} 感知: 威胁值[{threatBucket}~{threatBucket + 9}] " +
                  $"玩家可见[{_bb.IsPlayerVisible}] 玩家形态[{playerForm}] 自身形态[{ownForm}] " +
                  $"同形态友好[{_bb.IsPlayerSameForm}] 威胁记忆[{_bb.HasThreatMemory}]", this);

        _lastLogVisible = _bb.IsPlayerVisible;
        _lastLogSameForm = _bb.IsPlayerSameForm;
        _lastLogThreatBucket = threatBucket;
    }

    /// <summary>
    /// 威胁感知：视野扇形 + 听觉圆形过滤，结合目标移动状态更新威胁认知。
    /// 只负责"感知与记忆"，如何反应（逃跑/搜索/无视）由决策层决定。
    /// 玩家与自身同形态时视为友好：威胁快速消退，不产生威胁记忆。
    /// </summary>
    private void DetectThreats()
    {
        UpdatePlayerFormAwareness();

        float detectRadius = Mathf.Max(_visionRadius, _hearingRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectRadius, _threatLayer);

        float nearestDist = float.MaxValue;
        Transform nearest = null;
        Rigidbody2D nearestRb = null;

        foreach (Collider2D hit in hits)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist >= nearestDist) continue;

            // 听觉：全向圆形，半径内即可感知
            bool audible = dist <= _hearingRadius;
            // 视觉：扇形，需在视野距离内且位于前方视野锥内
            bool visible = false;
            if (!audible && dist <= _visionRadius)
            {
                Vector2 toTarget = (Vector2)(hit.transform.position - transform.position);
                float angle = Vector2.Angle(GetForwardDirection(), toTarget);
                visible = angle <= _visionHalfAngle;
            }

            if (audible || visible)
            {
                nearestDist = dist;
                nearest = hit.transform;
                nearestRb = hit.attachedRigidbody;
            }
        }

        _bb.IsPlayerVisible = nearest != null;

        if (_bb.IsPlayerVisible)
        {
            Vector2 toThreat = (Vector2)(nearest.position - transform.position);
            _bb.PlayerDirection = toThreat.normalized;
            _bb.PlayerDistance = nearestDist;

            if (_bb.IsPlayerSameForm)
            {
                // 同形态友好：威胁快速消退，不刷新威胁记忆
                _bb.ThreatLevel = Mathf.Max(_bb.ThreatLevel - _threatCalmRate * 3f * Time.deltaTime, 0f);
            }
            else
            {
                // 伪装判定：目标静止时威胁缓慢消退，移动时威胁快速上升
                float speed = nearestRb != null ? nearestRb.velocity.magnitude : 0f;
                bool isTargetMoving = speed > _moveSpeedThreshold;

                if (isTargetMoving)
                {
                    _bb.ThreatLevel = Mathf.Min(_bb.ThreatLevel + _threatRiseRate * Time.deltaTime, 100f);
                    _bb.LastKnownPlayerPos = nearest.position;
                    _bb.LastSeenPlayerTime = Time.time;
                }
                else
                {
                    _bb.ThreatLevel = Mathf.Max(_bb.ThreatLevel - _threatCalmRate * Time.deltaTime, 0f);
                    _bb.LastSeenPlayerTime = Time.time;
                    _bb.LastKnownPlayerPos = nearest.position;
                }
            }
        }
        else
        {
            // 失去感知：威胁随时间自然衰减，记忆过期后清空
            _bb.ThreatLevel = Mathf.Max(_bb.ThreatLevel - _threatDecayRate * Time.deltaTime, 0f);
            if (!_bb.HasThreatMemory)
                _bb.LastKnownPlayerPos = Vector2.zero;
        }

        // 感知数据写入完毕后，刷新带迟滞的威胁紧迫状态（供逃生/回撤决策使用）
        _bb.RefreshThreatUrgent();
    }

    /// <summary>
    /// 更新玩家形态感知：缓存 PlayerController，比较玩家当前形态与自身形态。
    /// 同形态（如玩家变为青蛙）时黑板标记友好，AI 不再产生威胁反应。
    /// </summary>
    private void UpdatePlayerFormAwareness()
    {
        if (_playerController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerController = player.GetComponent<PlayerController>();
        }

        FormType? playerForm = GetPlayerFormType();
        _bb.IsPlayerSameForm = playerForm.HasValue
            && _devourable != null
            && playerForm.Value == _devourable.GrantedForm;
    }

    /// <summary>
    /// 解析玩家当前形态。BaseForm.formType 序列化字段可能未在 Inspector 配置
    /// （默认枚举首值 Slime），因此优先按组件类型推断，与 PlayerController.ResolveFormType 一致。
    /// </summary>
    private FormType? GetPlayerFormType()
    {
        BaseForm active = _playerController != null ? _playerController.ActiveForm : null;
        if (active == null) return null;

        if (active is SlimeForm) return FormType.Slime;
        if (active is FrogForm) return FormType.Frog;
        if (active is BubbleFishForm) return FormType.BubbleFish;
        return active.FormType;
    }

    private void DetectFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _foodRadius, _foodLayer);

        float nearestDist = float.MaxValue;
        _bb.IsFoodDetected = false;
        _bb.NearestFood = null;

        foreach (Collider2D hit in hits)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                _bb.NearestFood = hit.transform;
                _bb.IsFoodDetected = true;
            }
        }

        if (_bb.IsFoodDetected)
        {
            Vector2 toFood = _bb.NearestFood.position - transform.position;
            _bb.FoodDirection = toFood.normalized;
            _bb.FoodDistance = nearestDist;
        }
    }

    private void DetectTerrain()
    {
        Vector2 forward = GetForwardDirection();

        // 前方地面检测（向下 + 前方）
        Vector2 groundCheckOrigin = (Vector2)transform.position + forward * 0.3f;
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down,
            _groundCheckDistance, _groundLayer);
        _bb.IsGroundedAhead = groundHit.collider != null;
        _bb.IsGapAhead = !_bb.IsGroundedAhead;

        // 前方墙壁检测
        Vector2 wallOrigin = (Vector2)transform.position + Vector2.up * 0.3f;
        RaycastHit2D wallHit = Physics2D.Raycast(wallOrigin, forward,
            _wallCheckDistance, _groundLayer);
        _bb.IsWallAhead = wallHit.collider != null;
    }

    private void DetectFellows()
    {
        NearbyFellows.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _fellowRadius, _fellowLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit.transform != transform)
                NearbyFellows.Add(hit.transform);
        }
    }

    /// <summary>
    /// 获取当前朝向的前方方向向量。
    /// 动物通过 SpriteRenderer.flipX 转向：flipX=true 朝左，false 朝右。
    /// 编辑器未运行时回退到 localScale 判断。
    /// </summary>
    public Vector2 GetForwardDirection()
    {
        if (_spriteRenderer != null)
            return _spriteRenderer.flipX ? Vector2.left : Vector2.right;
        return transform.localScale.x >= 0 ? Vector2.right : Vector2.left;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector2 forward = Application.isPlaying
            ? GetForwardDirection()
            : (transform.localScale.x >= 0 ? Vector2.right : Vector2.left);

        // 视野扇形
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        DrawVisionConeGizmo(forward);

        // 听觉圆形
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _hearingRadius);

        // 逃跑触发范围
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _fleeRadius);

        // 威胁解除范围（迟滞外环）
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, _safeRadius);

        // 食物探测范围
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, _foodRadius);

        // 同类探测范围
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, _fellowRadius);

        // 地形检测射线
        bool isGap = Application.isPlaying ? (_bb?.IsGapAhead ?? false) : false;
        bool isWall = Application.isPlaying ? (_bb?.IsWallAhead ?? false) : false;

        Gizmos.color = isGap ? Color.red : Color.green;
        Vector2 groundOrigin = (Vector2)transform.position + forward * 0.3f;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * _groundCheckDistance);

        Gizmos.color = isWall ? Color.red : Color.blue;
        Vector2 wallOrigin = (Vector2)transform.position + Vector2.up * 0.3f;
        Gizmos.DrawLine(wallOrigin, wallOrigin + forward * _wallCheckDistance);

        // 威胁记忆位置标记
        if (Application.isPlaying && _bb != null && _bb.HasThreatMemory && _bb.LastKnownPlayerPos != Vector2.zero)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.DrawWireSphere(_bb.LastKnownPlayerPos, 0.3f);
            Gizmos.DrawLine(transform.position, _bb.LastKnownPlayerPos);
        }

        // 最近食物连线
        if (Application.isPlaying && _bb != null && _bb.IsFoodDetected && _bb.NearestFood != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _bb.NearestFood.position);
        }
    }

    private void DrawVisionConeGizmo(Vector2 forward)
    {
        const int segments = 20;
        float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

        Vector2 prevPoint = transform.position;
        for (int i = 0; i <= segments; i++)
        {
            float angle = baseAngle + Mathf.Lerp(-_visionHalfAngle, _visionHalfAngle, (float)i / segments);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 point = (Vector2)transform.position + dir * _visionRadius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // 扇形两条边界线
        float minAngle = (baseAngle - _visionHalfAngle) * Mathf.Deg2Rad;
        float maxAngle = (baseAngle + _visionHalfAngle) * Mathf.Deg2Rad;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position +
            new Vector2(Mathf.Cos(minAngle), Mathf.Sin(minAngle)) * _visionRadius);
        Gizmos.DrawLine(transform.position, (Vector2)transform.position +
            new Vector2(Mathf.Cos(maxAngle), Mathf.Sin(maxAngle)) * _visionRadius);
    }
#endif
}
