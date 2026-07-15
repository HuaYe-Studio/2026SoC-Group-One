using UnityEngine;

/// <summary>
/// [FSM] 动物AI基类，挂载到动物NPC的GameObject上。
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
    [SerializeField] private float _detectionRadius = 6f;
    [SerializeField] private float _fleeSafeDistance = 10f;
    [SerializeField] private LayerMask _playerLayer;

    [Header("Flee")]
    [SerializeField] private float _fleeSpeedMultiplier = 1.5f;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;

    private Vector2 _spawnPosition;

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
    /// 当前是否检测到玩家在探测范围内。
    /// </summary>
    public bool IsPlayerDetected { get; private set; }

    /// <summary>
    /// 玩家相对于动物的方向（归一化），仅在IsPlayerDetected为true时有效。
    /// </summary>
    public Vector2 PlayerDirection { get; private set; }

    /// <summary>
    /// 到玩家的距离，仅在IsPlayerDetected为true时有效。
    /// </summary>
    public float PlayerDistance { get; private set; }

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

    protected virtual void Awake()
    {
        Fsm = GetComponent<FSM>();
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        _spawnPosition = transform.position;

        FindPlayer();
        RegisterStates();
        Fsm.ChangeState<IdleState>();
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
            return;
        }

        UpdatePlayerDetection();
    }

    private void UpdatePlayerDetection()
    {
        if (_playerTransform == null)
        {
            IsPlayerDetected = false;
            return;
        }

        Vector2 toPlayer = _playerTransform.position - transform.position;
        float distance = toPlayer.magnitude;

        PlayerDirection = toPlayer.normalized;
        PlayerDistance = distance;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, _detectionRadius, _playerLayer);
        IsPlayerDetected = hit != null;
    }

    /// <summary>
    /// 朝指定方向水平移动，自动处理朝向翻转。
    /// </summary>
    /// <param name="direction">水平方向（正=右，负=左）</param>
    /// <param name="speedMultiplier">速度倍率，默认1</param>
    public void MoveHorizontal(float direction, float speedMultiplier = 1f)
    {
        _rb.velocity = new Vector2(direction * _moveSpeed * speedMultiplier, _rb.velocity.y);

        if (_spriteRenderer != null && Mathf.Abs(direction) > 0.05f)
            _spriteRenderer.flipX = direction < 0;
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
