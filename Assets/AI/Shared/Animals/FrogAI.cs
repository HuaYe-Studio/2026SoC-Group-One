using UnityEngine;

/// <summary>
/// 青蛙AI：继承 AnimalBase，以跳跃方式移动。
/// 覆写 PerformMove 实现跳跃式移动。
/// </summary>
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

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：Int 枚举，0=Idle 1=Jump 2=Rest 3=Flee 4=Prey
    private const string AnimStateParam = "FROG_AnimState";

    private Collider2D _collider;
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

    // 覆写基类食物属性，数据来源于 Blackboard（由 EnvironmentMonitor 写入）
    public override bool IsFoodDetected => Board.IsFoodDetected;
    public override Vector2 FoodDirection => Board.FoodDirection;
    public override float FoodDistance => Board.FoodDistance;

    protected override void Awake()
    {
        _collider = GetComponent<Collider2D>();

        // 暴露 Animator 给基类，供吞噬系统等外部调用
        if (_animator != null)
            Animator = _animator;

        base.Awake();
    }

    private void FixedUpdate()
    {
        PerformGroundCheck();
    }

    /// <summary>
    /// 地面检测：从碰撞体底部向下做 BoxCast。
    /// 排除自身碰撞体和触发器，防止 GroundLayer 配置失误（如勾了自身层）导致"着地"恒真。
    /// </summary>
    private void PerformGroundCheck()
    {
        float width = _collider != null ? _collider.bounds.size.x * _groundCheckWidth : 0.4f;

        Vector2 origin = new Vector2(transform.position.x,
            _collider != null ? _collider.bounds.min.y : transform.position.y - 0.5f);

        Vector2 size = new Vector2(width, _groundCheckHeight);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_groundLayer);
        filter.useTriggers = false;

        int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, filter, _groundHits, 0.05f);

        IsGrounded = false;
        for (int i = 0; i < count; i++)
        {
            if (_groundHits[i].collider != _collider)
            {
                IsGrounded = true;
                break;
            }
        }
    }

    /// <summary>
    /// 青蛙以跳跃方式移动：着地时朝目标方向跳跃，空中不施加额外水平力。
    /// 逃跑/捕食动画由对应行为树节点通过 PerformHop 指定 animName 驱动。
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
    /// <param name="animName">跳跃期间播放的动画状态名，默认 Jump</param>
    public void PerformHop(float direction, float speedMultiplier = 1f, string animName = "Jump")
    {
        NotifyMoveCommand();

        Rb.velocity = new Vector2(
            direction * _hopForwardSpeed * speedMultiplier,
            _hopForce
        );

        if (SpriteRenderer != null && Mathf.Abs(direction) > 0.05f)
            SpriteRenderer.flipX = direction < 0;

        PlayAnimation(animName);
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
