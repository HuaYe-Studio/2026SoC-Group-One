using UnityEngine;

/// <summary>
/// 泡泡鱼 AI：继承 AnimalBase，适配水生环境的游动移动。
/// 与陆地动物不同，泡泡鱼无视重力，可以在水中自由游动。
/// 覆写 PerformMove 实现平滑游动，覆写 RegisterStates 注册水生专属状态。
/// </summary>
[RequireComponent(typeof(FSM))]
[RequireComponent(typeof(Rigidbody2D))]
public class BubbleFishAI : AnimalBase
{
    [Header("Swim Settings")]
    [SerializeField] private float _swimSpeed = 2f;
    [SerializeField] private float _swimVerticalDrift = 0.5f; // 游动时的轻微上下漂移

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：与 BubbleFishForm 的 BubbleState 枚举保持一致
    private const string AnimStateParam = "BubbleState";

    private float _driftTimer;
    private float _driftDirectionY;

    /// <summary>
    /// 泡泡鱼始终处于"水"中，无需地面检测。
    /// </summary>
    public override bool IsGrounded { get => true; protected set { } }

    protected override void Awake()
    {
        base.Awake();

        // 泡泡鱼不受重力影响；手动控制速度，drag 必须为 0，否则物理阻尼会抵消 velocity
        if (Rb != null)
        {
            Rb.gravityScale = 0f;
            Rb.drag = 0f;
        }
    }

    /// <summary>
    /// 泡泡鱼的移动方式：平滑游动，带有轻微的上下漂移。
    /// </summary>
    public override void PerformMove(float direction, float speedMultiplier = 1f)
    {
        // 水平游动
        float horizontalVelocity = direction * _swimSpeed * speedMultiplier;

        // 轻微的垂直漂移，让游动看起来更自然
        _driftTimer += Time.deltaTime;
        if (_driftTimer > 1.5f)
        {
            _driftTimer = 0f;
            _driftDirectionY = Random.Range(-1f, 1f);
        }

        float verticalVelocity = _driftDirectionY * _swimVerticalDrift;

        Rb.velocity = new Vector2(horizontalVelocity, verticalVelocity);

        // 翻转朝向
        if (SpriteRenderer != null && Mathf.Abs(direction) > 0.05f)
            SpriteRenderer.flipX = direction < 0;
    }

    /// <summary>
    /// 根据状态名设置 Animator 的 BubbleState 整数参数。
    /// 与 BubbleFishForm 的 BubbleState 枚举值保持一致：
    /// 0=Shrunk（收缩/闲置）, 1=Expanding（膨胀/逃跑）, 2=Expanded（完全膨胀）, 3=Shrinking（收缩中/受击）
    /// </summary>
    public override void PlayAnimation(string stateName)
    {
        if (_animator == null) return;

        int state = 0; // 默认 Shrunk/Idle
        switch (stateName)
        {
            case "Idle": state = 0; break;
            case "Flee": state = 1; break;
            case "Stunned": state = 3; break;
            case "Expanded": state = 2; break;
        }

        _animator.SetInteger(AnimStateParam, state);
    }

    /// <summary>
    /// 注册泡泡鱼专用状态集合：
    /// Idle → Patrol（游动）→ 循环
    /// 玩家靠近 → Flee（膨胀加速游走）
    /// 被吞噬/受击 → Stunned（0.5s 僵直）
    /// </summary>
    protected override void RegisterStates()
    {
        Fsm.RegisterState(new IdleState(Fsm, this, () => Fsm.ChangeState<PatrolState>()));
        Fsm.RegisterState(new PatrolState(Fsm, this));
        Fsm.RegisterState(new FleeState(Fsm, this));
        Fsm.RegisterState(new StunnedState(Fsm, this, 0.5f));
    }

    /// <summary>
    /// 被吞噬时由外部调用，切换到眩晕状态（FSM 模式）。
    /// 如果当前由 BT 驱动，则直接设置标记供 BT 条件读取。
    /// </summary>
    public void OnDevoured()
    {
        // 两种模式兼容：FSM 切状态，BT 设标记
        Fsm.ChangeState<StunnedState>();
        IsDevoured = true;
    }

    /// <summary>
    /// 是否刚被吞噬（供 BT 条件节点读取，读取后自动清除）。
    /// </summary>
    public bool IsDevoured { get; private set; }

    /// <summary>
    /// 清除被吞噬标记，由 BT 眩晕节点在完成后调用。
    /// </summary>
    public void ClearDevoured()
    {
        IsDevoured = false;
    }

    /// <summary>
    /// 游动速度（供 BT 节点读取）。
    /// </summary>
    public float SwimSpeed => _swimSpeed;

    /// <summary>
    /// 垂直漂移幅度（供 BT 节点读取）。
    /// </summary>
    public float SwimVerticalDrift => _swimVerticalDrift;

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // 绘制巡游半径
        Gizmos.color = Color.cyan;
        Vector2 spawnPos = Application.isPlaying ? SpawnPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(spawnPos, PatrolRadius);
    }
#endif
}
