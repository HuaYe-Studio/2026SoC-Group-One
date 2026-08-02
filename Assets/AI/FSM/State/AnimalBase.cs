using UnityEngine;

/// <summary>
/// 动物AI基类，挂载到动物NPC的GameObject上。
/// 负责组装FSM、注册状态、提供感知与移动等共用能力给各子状态使用。
/// 使用方式：子类继承后在Awake中配置各状态的构造参数。
/// </summary>
[RequireComponent(typeof(FSM))]
[RequireComponent(typeof(Rigidbody2D))]
public class AnimalBase : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _patrolPauseMin = 1.5f;
    [SerializeField] private float _patrolPauseMax = 3f;
    [SerializeField] private float _patrolRadius = 3f;

    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 4.5f;
    [SerializeField] private float _fleeSafeDistance = 10f;
    [SerializeField] private LayerMask _playerLayer;

    [Header("Flee")]
    [SerializeField] private float _fleeSpeedMultiplier = 1.5f;

    [Header("Anti-Stuck (防卡死)")]
    [Tooltip("卡死判定检查间隔（秒）：每过此间隔采样一次位移")]
    [SerializeField] private float _stuckCheckInterval = 1f;
    [Tooltip("卡死判定位移阈值（米）：采样间隔内位移小于此值，且期间下达过移动指令，视为卡死")]
    [SerializeField] private float _stuckMoveThreshold = 0.3f;
    [Tooltip("脱困冷却（秒）：一次脱困尝试后，间隔多久才允许再次判定卡死")]
    [SerializeField] private float _stuckRetryCooldown = 3f;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;

    private Vector2 _spawnPosition;

    // ---- 防卡死状态 ----
    private float _lastMoveCommandTime = float.NegativeInfinity; // 最近一次移动指令时间戳
    private Vector2 _lastStuckCheckPos;                          // 上一次卡死采样位置
    private float _nextStuckCheckTime;                           // 下次采样时间
    private float _stuckRetryUntil = float.NegativeInfinity;     // 脱困冷却截止时间
    private bool _isStuck;

    protected FSM Fsm { get; private set; }

    public float MoveSpeed => _moveSpeed;
    public float PatrolPauseMin => _patrolPauseMin;
    public float PatrolPauseMax => _patrolPauseMax;
    public float PatrolRadius => _patrolRadius;
    public float DetectionRadius => _detectionRadius;
    public float FleeSafeDistance => _fleeSafeDistance;
    public LayerMask PlayerLayer => _playerLayer;
    public float FleeSpeedMultiplier => _fleeSpeedMultiplier;
    public Rigidbody2D Rb => _rb;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    public Vector2 SpawnPosition => _spawnPosition;

    /// <summary>
    /// 是否处于卡死状态：最近下达过移动指令，但采样间隔内几乎没位移。
    /// 由 Update 中的卡死检测自动更新，供行为树/状态机触发脱困行为。
    /// </summary>
    public bool IsStuck => _isStuck;

    /// <summary>
    /// AI 认知黑板：所有语义化感知与内部状态统一存放处。
    /// </summary>
    public Blackboard Board { get; private set; } = new Blackboard();

    /// <summary>
    /// 环境感知器，延迟获取。青蛙和鱼都挂载 EnvironmentMonitor。
    /// </summary>
    public EnvironmentMonitor Monitor { get; private set; }

    /// <summary>
    /// 当前动物的 Animator 引用。子类在 Awake 中赋值。
    /// 供外部系统（如吞噬处理器）在 TimeScale=0 时临时切换更新模式并播放动画。
    /// </summary>
    public Animator Animator { get; protected set; }

    /// <summary>
    /// 当前是否检测到玩家在探测范围内。
    /// 数据来源：EnvironmentMonitor 写入 Blackboard。
    /// </summary>
    public bool IsPlayerDetected => Board.IsPlayerVisible;

    /// <summary>
    /// 玩家相对于动物的方向（归一化），仅在 IsPlayerDetected 为 true 时有效。
    /// </summary>
    public Vector2 PlayerDirection => Board.PlayerDirection;

    /// <summary>
    /// 到玩家的距离，仅在 IsPlayerDetected 为 true 时有效。
    /// </summary>
    public float PlayerDistance => Board.PlayerDistance;

    /// <summary>
    /// 当前是否检测到食物。默认返回false，子类可覆写以接入EnvironmentMonitor。
    /// </summary>
    public virtual bool IsFoodDetected => false;

    /// <summary>
    /// 当前是否着地。默认返回true，跳跃类动物覆写。
    /// </summary>
    public virtual bool IsGrounded { get; protected set; } = true;

    /// <summary>
    /// 最近食物的方向（归一化），仅在IsFoodDetected为true时有效。
    /// </summary>
    public virtual Vector2 FoodDirection => Vector2.zero;

    /// <summary>
    /// 到最近食物的距离，仅在IsFoodDetected为true时有效。
    /// </summary>
    public virtual float FoodDistance => 0f;

    /// <summary>
    /// 播放动画。默认空实现，子类可覆写以接入Animator。
    /// </summary>
    /// <param name="stateName">动画状态名或Trigger名</param>
    public virtual void PlayAnimation(string stateName) { }

    /// <summary>
    /// 被吞噬时由 DevourableAnimal 调用：清空威胁认知并进入眩晕。
    /// 防止被吐出后 AI 立即因贴脸玩家而受惊蹿出。
    /// </summary>
    /// <param name="stunDuration">眩晕时长（秒），从时间恢复流动后起算</param>
    public virtual void OnDevoured(float stunDuration)
    {
        Board.ClearThreat();
        Board.StunUntilTime = Time.time + stunDuration;
        StopMoving();

        // FSM 驱动时同步切到眩晕状态（BT 驱动时 FSM 已禁用，由黑板眩晕标记生效）
        if (Fsm != null && Fsm.enabled)
            Fsm.ChangeState<StunnedState>();
    }

    protected virtual void Awake()
    {
        Fsm = GetComponent<FSM>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Monitor = GetComponent<EnvironmentMonitor>();

        _spawnPosition = transform.position;
        _lastStuckCheckPos = transform.position;

        FindPlayer();
        RegisterStates();
        Fsm.ChangeState<IdleState>();
    }

    /// <summary>
    /// 记录一次移动指令（由 MoveHorizontal / 子类跳跃原语调用）。
    /// 用于卡死判定：只有"想动却没动"才算卡死，静置/休息不算。
    /// </summary>
    public void NotifyMoveCommand()
    {
        _lastMoveCommandTime = Time.time;
    }

    /// <summary>
    /// 通知一次脱困尝试已执行（由 BTUnstickAction 调用）。
    /// 清除卡死标记并进入脱困冷却，避免行为树对同一卡死状态无限重试。
    /// </summary>
    public void NotifyUnstickAttempt()
    {
        _isStuck = false;
        _stuckRetryUntil = Time.time + _stuckRetryCooldown;
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
    }

    /// <summary>
    /// 子类覆写此方法以注册自定义状态集合。
    /// 默认注册 Idle / Patrol / Flee 三种基础状态。
    /// </summary>
    protected virtual void RegisterStates()
    {
        Fsm.RegisterState(new IdleState(Fsm, this, () => Fsm.ChangeState<PatrolState>()));
        Fsm.RegisterState(new PatrolState(Fsm, this));
        Fsm.RegisterState(new FleeState(Fsm, this));
    }

    protected virtual void Update()
    {
        if (_playerTransform == null)
        {
            FindPlayer();
        }

        UpdateStuckDetection();
    }

    /// <summary>
    /// 卡死检测：按采样间隔比较位移。
    /// 仅当"最近下达过移动指令"时参与判定，否则视为正常静置。
    /// 脱困冷却期间跳过检测，给脱困行为留出生效时间。
    /// </summary>
    private void UpdateStuckDetection()
    {
        // 脱困冷却中：不重新判定卡死
        if (Time.time < _stuckRetryUntil)
        {
            _isStuck = false;
            return;
        }

        if (Time.time < _nextStuckCheckTime)
            return;
        _nextStuckCheckTime = Time.time + _stuckCheckInterval;

        // 没有移动指令（静置/休息中）不判定卡死
        if (Time.time - _lastMoveCommandTime > _stuckCheckInterval)
        {
            _isStuck = false;
            return;
        }

        float moved = Vector2.Distance(transform.position, _lastStuckCheckPos);
        _lastStuckCheckPos = transform.position;

        _isStuck = moved < _stuckMoveThreshold;
    }

    /// <summary>
    /// 朝指定方向水平移动，自动处理朝向翻转。
    /// </summary>
    /// <param name="direction">水平方向（正=右，负=左）</param>
    /// <param name="speedMultiplier">速度倍率，默认1</param>
    public void MoveHorizontal(float direction, float speedMultiplier = 1f)
    {
        NotifyMoveCommand();

        _rb.velocity = new Vector2(direction * _moveSpeed * speedMultiplier, _rb.velocity.y);

        if (_spriteRenderer != null && Mathf.Abs(direction) > 0.05f)
        {
            // 方向与上次朝向一致时不更新 flipX，防止每帧方向在 0 附近抖动导致翻转抽搐
            bool wantFlipX = direction < 0;
            if (wantFlipX != _spriteRenderer.flipX)
                _spriteRenderer.flipX = wantFlipX;
        }
    }

    /// <summary>
    /// 停止水平移动（保留垂直速度以保持重力效果）。
    /// </summary>
    public void StopMoving()
    {
        _rb.velocity = new Vector2(0f, _rb.velocity.y);
    }

    /// <summary>
    /// 执行移动动作。子类可覆写以定制移动方式（如青蛙跳跃代替行走）。
    /// 默认实现为水平行走。
    /// </summary>
    /// <param name="direction">水平方向（正=右，负=左）</param>
    /// <param name="speedMultiplier">速度倍率，默认1</param>
    public virtual void PerformMove(float direction, float speedMultiplier = 1f)
    {
        MoveHorizontal(direction, speedMultiplier);
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        Gizmos.color = Color.green;
        Vector2 spawnPos = Application.isPlaying ? _spawnPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(spawnPos, _patrolRadius);
    }
#endif
}
