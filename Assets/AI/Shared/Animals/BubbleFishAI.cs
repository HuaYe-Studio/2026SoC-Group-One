using UnityEngine;

/// <summary>
/// 泡泡鱼 AI：继承 AnimalBase，适配水生环境的游动移动。
/// 与陆地动物不同，泡泡鱼无视重力，可以在水中自由游动。
/// 覆写 PerformMove 实现平滑游动。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BubbleFishAI : AnimalBase
{
    [Header("Swim Settings")]
    [SerializeField] private float _swimSpeed = 2f;
    [SerializeField] private float _swimVerticalDrift = 0.5f; // 游动时的轻微上下漂移

    [Tooltip("垂直速度比例：上下游动速度 = 水平速度 × 此系数。鱼体体积感——上下游动明显慢于水平")]
    [SerializeField, Range(0.05f, 1f)] private float _verticalSpeedRatio = 0.4f;

    [Tooltip("上升减速比例：上升速度在垂直基础上再 × 此系数（上升比下降更慢，模拟浮力/体积感）")]
    [SerializeField, Range(0.05f, 1f)] private float _ascendSpeedRatio = 0.5f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：与 BubbleFishForm 的 BubbleState 枚举保持一致
    private const string AnimStateParam = "BubbleState";

    // 独立游泳动画参数：0=Idle 1=SwimForward 2=SwimUp 3=SwimDown
    // 与玩家形态的 BubbleState 解耦，供路径跟随等 NPC 行为使用
    private const string SwimStateParam = "SwimState";

    private float _driftTimer;
    private float _driftDirectionY;

    /// <summary>
    /// 泡泡鱼始终处于"水"中，无需地面检测。
    /// </summary>
    public override bool IsGrounded { get => true; protected set { } }

    protected override void Awake()
    {
        base.Awake();

        // 暴露 Animator 给基类，供吞噬系统等外部调用
        if (_animator != null)
            Animator = _animator;

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
        NotifyMoveCommand();

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
    /// 朝任意方向游动（路径跟随等垂直+水平混合移动用）。
    /// 各向异性速度：水平分量按 _swimSpeed；垂直（上下）分量 = 水平 × _verticalSpeedRatio，
    /// 上升再 × _ascendSpeedRatio（上升比下降更慢），体现鱼体的体积感——上下游动明显慢于水平游动。
    /// </summary>
    /// <param name="direction">游动方向（归一化向量）</param>
    /// <param name="speedMultiplier">速度倍率</param>
    public void Swim(Vector2 direction, float speedMultiplier = 1f)
    {
        NotifyMoveCommand();

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        // 各向异性：水平 / 垂直 / 上升(更慢) 分开计算
        float horizontalVelocity = direction.x * _swimSpeed * speedMultiplier;
        float verticalSpeed = _swimSpeed * _verticalSpeedRatio * speedMultiplier;
        if (direction.y > 0f)
            verticalSpeed *= _ascendSpeedRatio; // 上升更慢，模拟浮力阻力
        float verticalVelocity = direction.y * verticalSpeed;

        Rb.velocity = new Vector2(horizontalVelocity, verticalVelocity);

        // 翻转朝向：只有水平分量明显时才翻转，防止纯上浮时抖动
        if (SpriteRenderer != null && Mathf.Abs(direction.x) > 0.2f)
            SpriteRenderer.flipX = direction.x < 0;
    }

    /// <summary>
    /// 根据状态名设置 Animator 动画参数。
    /// 游泳类状态（SwimUp/SwimDown/SwimForward）写入独立的 SwimState 参数，
    /// 其余状态保持原有的 BubbleState 映射不变。
    /// </summary>
    public override void PlayAnimation(string stateName)
    {
        if (_animator == null) return;

        // 游泳类状态：写入独立的 SwimState 参数
        switch (stateName)
        {
            case "SwimForward": SetSwimState(1); return;
            case "SwimUp": SetSwimState(2); return;
            case "SwimDown": SetSwimState(3); return;
        }

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
    /// 设置游泳动画参数，仅在该值变化时写入，避免每帧重复 SetInteger。
    /// </summary>
    private void SetSwimState(int value)
    {
        if (_animator.GetInteger(SwimStateParam) == value)
            return;

        _animator.SetInteger(SwimStateParam, value);
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
