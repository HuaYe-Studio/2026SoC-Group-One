using System.Collections;
using UnityEngine;

/// <summary>
/// 青蛙AI：继承 AnimalBase，以跳跃方式移动。
/// 使用 Forage / Rest 状态替代默认的 Patrol 状态，
/// 覆写 PerformMove 实现跳跃式移动。
/// </summary>
[RequireComponent(typeof(FSM))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnvironmentMonitor))]
public class FrogAI : AnimalBase
{
    [Header("Jump")]
    [SerializeField] private float _hopForce = 5f;
    [SerializeField] private float _hopForwardSpeed = 2.35f;

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckWidth = 0.5f;
    [SerializeField] private float _groundCheckHeight = 0.08f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Idle Fidget")]
    [SerializeField] private float _idleHopForce = 5f;
    [SerializeField] private float _idleHopSpeed = 1.5f;
    [SerializeField] private float _idleHopIntervalMin = 1.5f;
    [SerializeField] private float _idleHopIntervalMax = 4f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：Int 枚举，0=Idle 1=Jump 2=Rest 3=Flee 4=Prey
    private const string AnimStateParam = "FROG_AnimState";

    private EnvironmentMonitor _monitor;
    private float _nextIdleHopTime;
    private bool _hasIdleHopped;

    /// <summary>
    /// 环境监视器引用，供外部查询地形、同类等信息。
    /// </summary>
    public EnvironmentMonitor Monitor => _monitor;

    // 覆写基类食物属性，数据来源于 EnvironmentMonitor
    public override bool IsFoodDetected => _monitor != null && _monitor.IsFoodDetected;
    public override Vector2 FoodDirection => _monitor != null ? _monitor.FoodDirection : Vector2.zero;
    public override float FoodDistance => _monitor != null ? _monitor.FoodDistance : 0f;

    protected override void Awake()
    {
        _monitor = GetComponent<EnvironmentMonitor>();

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

        PlayAnimation("Jump");
    }

    /// <summary>
    /// 根据状态名设置 Animator 的 AnimState 整数参数。
    /// </summary>
    public override void PlayAnimation(string stateName)
    {
        if (_animator == null) return;

        int state = 0;
        switch (stateName)
        {
            case "Jump": state = 1; break;
            case "Rest": state = 2; break;
            case "Flee": state = 3; break;
            case "Prey": state = 4; break;
        }

        _animator.SetInteger(AnimStateParam, state);
    }

    /// <summary>
    /// 青蛙使用觅食+休息+捕食+逃跑状态，不使用默认的巡逻状态。
    /// 闲置时会随机小幅跳跃。
    /// </summary>
    protected override void RegisterStates()
    {
        ResetIdleHopTimer();
        Fsm.RegisterState(new IdleState(Fsm, this, () => Fsm.ChangeState<ForageState>(),
            null, null));
        Fsm.RegisterState(new ForageState(Fsm, this));
        Fsm.RegisterState(new RestState(Fsm, this));
        Fsm.RegisterState(new PounceState(Fsm, this));
        Fsm.RegisterState(new FleeState(Fsm, this));
    }

    /// <summary>
    /// 重置闲置跳跃计时器和标记（每次进入闲置时调用）。
    /// </summary>
    private void ResetIdleHopTimer()
    {
        _hasIdleHopped = false;
        _nextIdleHopTime = Time.time + Random.Range(_idleHopIntervalMin, _idleHopIntervalMax);
    }

    /// <summary>
    /// 闲置时的随机小跳。由 IdleState 每帧回调触发。每次闲置期只跳一次。
    /// </summary>
    private void TryIdleHop()
    {
        if (_hasIdleHopped || !IsGrounded)
            return;

        if (Time.time < _nextIdleHopTime)
            return;

        // 随机方向小幅跳跃
        float direction = Random.value < 0.5f ? -1f : 1f;
        Rb.velocity = new Vector2(direction * _idleHopSpeed, _idleHopForce);

        if (SpriteRenderer != null)
            SpriteRenderer.flipX = direction < 0;

        PlayAnimation("Jump");
        _hasIdleHopped = true;

        // 跳完后切回 Idle 动画，防止 AnimState 卡在 Jump
        StartCoroutine(ResetToIdleAnimation());
    }

    /// <summary>
    /// 等待落地后切回 Idle 动画，保证空中全程显示跳跃动画。
    /// </summary>
    private System.Collections.IEnumerator ResetToIdleAnimation()
    {
        // 等青蛙落地再切动画
        while (!IsGrounded)
            yield return null;

        if (Fsm.CurrentStateType == typeof(IdleState))
            PlayAnimation("Idle");
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
