using UnityEngine;

/// <summary>
/// 青蛙AI：继承 AnimalBase，以跳跃方式移动。
/// 使用 Forage / Rest 状态替代默认的 Patrol 状态，
/// 覆写 PerformMove 实现跳跃式移动。
/// </summary>
[RequireComponent(typeof(FSM))]
[RequireComponent(typeof(Rigidbody2D))]
public class FrogAI : AnimalBase
{
    [Header("Hop")]
    [SerializeField] private float _hopForce = 9f;
    [SerializeField] private float _hopForwardSpeed = 3.5f;

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckWidth = 0.5f;
    [SerializeField] private float _groundCheckHeight = 0.08f;
    [SerializeField] private LayerMask _groundLayer;

    private const float AirControlThreshold = 0.1f;

    [Header("Idle Fidget")]
    [SerializeField] private float _idleHopForce = 5f;
    [SerializeField] private float _idleHopSpeed = 1.5f;
    [SerializeField] private float _idleHopIntervalMin = 1.5f;
    [SerializeField] private float _idleHopIntervalMax = 4f;

    private float _idleHopTimer;
    private float _nextIdleHopTime;

    /// <summary>
    /// 当前是否着地。
    /// </summary>
    public bool IsGrounded { get; private set; }

    private bool _isInAir;

    protected override void Awake()
    {
        base.Awake();
    }

    private void FixedUpdate()
    {
        PerformGroundCheck();
    }

    private void PerformGroundCheck()
    {
        Collider2D col = GetComponent<Collider2D>();
        float width = col != null ? col.bounds.size.x * _groundCheckWidth : 0.4f;

        Vector2 origin = new Vector2(transform.position.x,
            col != null ? col.bounds.min.y : transform.position.y - 0.5f);

        Vector2 size = new Vector2(width, _groundCheckHeight);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.05f, _groundLayer);

        _isInAir = !hit && Rb.velocity.y > AirControlThreshold;
        IsGrounded = hit.collider != null;
    }

    /// <summary>
    /// 青蛙以跳跃方式移动：着地时朝目标方向跳跃，空中不施加额外水平力。
    /// </summary>
    public override void PerformMove(float direction, float speedMultiplier = 1f)
    {
        if (!IsGrounded)
            return;

        PerformHop(direction, speedMultiplier);
    }

    /// <summary>
    /// 执行一次跳跃：同时施加水平速度和垂直起跳力。
    /// </summary>
    /// <param name="direction">跳跃水平方向（正=右，负=左）</param>
    /// <param name="speedMultiplier">速度倍率</param>
    public void PerformHop(float direction, float speedMultiplier = 1f)
    {
        Rb.velocity = new Vector2(
            direction * _hopForwardSpeed * speedMultiplier,
            _hopForce
        );

        if (SpriteRenderer != null && Mathf.Abs(direction) > 0.05f)
            SpriteRenderer.flipX = direction < 0;
    }

    /// <summary>
    /// 青蛙使用觅食+休息+逃跑状态，不使用默认的巡逻状态。
    /// 闲置时会随机小幅跳跃。
    /// </summary>
    protected override void RegisterStates()
    {
        ResetIdleHopTimer();
        Fsm.RegisterState(new IdleState(Fsm, this, () => Fsm.ChangeState<ForageState>(), TryIdleHop));
        Fsm.RegisterState(new ForageState(Fsm, this));
        Fsm.RegisterState(new RestState(Fsm, this));
        Fsm.RegisterState(new FleeState(Fsm, this));
    }

    /// <summary>
    /// 重置闲置跳跃计时器（进入闲置时调用）。
    /// </summary>
    private void ResetIdleHopTimer()
    {
        _nextIdleHopTime = Time.time + Random.Range(_idleHopIntervalMin, _idleHopIntervalMax);
    }

    /// <summary>
    /// 闲置时的随机小跳。由 IdleState 每帧回调触发。
    /// </summary>
    private void TryIdleHop()
    {
        if (!IsGrounded)
            return;

        if (Time.time < _nextIdleHopTime)
            return;

        // 随机方向小幅跳跃
        float direction = Random.value < 0.5f ? -1f : 1f;
        Rb.velocity = new Vector2(direction * _idleHopSpeed, _idleHopForce);

        if (SpriteRenderer != null)
            SpriteRenderer.flipX = direction < 0;

        _nextIdleHopTime = Time.time + Random.Range(_idleHopIntervalMin, _idleHopIntervalMax);
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Collider2D col = GetComponent<Collider2D>();
        float width = col != null ? col.bounds.size.x * _groundCheckWidth : 0.4f;
        Vector2 origin = new Vector2(transform.position.x,
            col != null ? col.bounds.min.y : transform.position.y - 0.5f);
        Vector2 size = new Vector2(width, _groundCheckHeight);

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(origin, size);
    }
#endif
}
