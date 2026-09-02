using UnityEngine;

/// <summary>
/// [BT] 受伤反馈节点：动物受伤瞬间执行的"弹跳 + 横向位移"动作。
/// 由 AnimalHurtFeedback 维护受伤状态（IsHurting / HurtDirection），本节点负责执行动作。
/// - 垂直弹跳：v = sqrt(2·g·h)，高度 = AnimalHurtFeedback.HopHeight（默认 1 米）；
/// - 横向位移：朝远离伤害源方向，探测前方可通行格（复用 NavGrid2D，Spike 已标记 blocked），
///   被墙/尖刺挡则换反方向，两侧都堵则原地垂直弹跳；
/// - 完成：落地上升沿（跳跃动物）或超时（行走动物/鱼等永不着地）后返回 Success，自动恢复原行为。
/// </summary>
public class BTHurtFeedbackAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly AnimalHurtFeedback _feedback;

    private bool _hasHopped;
    private bool _wasGroundedLastFrame;
    private float _startTime;

    private const float ProbeDistance = 1.0f;   // 安全方向探测距离（米）
    private const float MaxDuration = 0.5f;     // 位移超时（秒）：覆盖行走动物/鱼等不落地的情况

    public BTHurtFeedbackAction(AnimalBase animal, AnimalHurtFeedback feedback)
    {
        _animal = animal;
        _feedback = feedback;
    }

    protected override State DoTick()
    {
        // 首次进入：执行弹跳 + 横向位移
        if (!_hasHopped)
        {
            _hasHopped = true;
            _wasGroundedLastFrame = _animal.IsGrounded;
            _startTime = Time.time;
            PerformHurtHop();
            return State.Running;
        }

        // 完成条件 1：落地上升沿（跳跃动物，如青蛙）
        bool grounded = _animal.IsGrounded;
        bool justLanded = grounded && !_wasGroundedLastFrame;
        _wasGroundedLastFrame = grounded;

        // 完成条件 2：超时（行走动物/鱼等永不着地，靠时间兜底）
        bool timedOut = Time.time - _startTime >= MaxDuration;

        if (justLanded || timedOut)
        {
            _feedback.EndHurt();
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    /// <summary>执行受伤弹跳：垂直弹跳 + 横向位移到安全方向。</summary>
    private void PerformHurtHop()
    {
        float direction = ResolveSafeDirection(_feedback.HurtDirection);

        // 垂直弹跳：v = sqrt(2·g·h)，使弹跳高度 = HopHeight
        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * _feedback.HopHeight);

        // 直接用 Rigidbody2D 施加速度，对所有动物（青蛙/羊/蜘蛛/鱼）通用。
        // 横向用受伤逃离速度（HurtFleeSpeed），显著大于普通 MoveSpeed，保证瞬间脱离伤害源而不是"原地弹跳落回刺上"。
        _animal.Rb.velocity = new Vector2(direction * _feedback.HurtFleeSpeed, jumpVelocity);
        _animal.NotifyMoveCommand();

        // 朝向与逃离方向一致，避免"往右逃却朝左看"
        if (_animal.SpriteRenderer != null && Mathf.Abs(direction) > 0.05f)
            _animal.SpriteRenderer.flipX = direction < 0f;
    }

    /// <summary>
    /// 解析安全位移方向：首选远离伤害源方向；前方被挡（墙/尖刺）则换反方向；
    /// 两侧都被挡也退回首选方向（绝不返回 0），保证受伤时一定水平位移离开伤害源，
    /// 而不是原地垂直弹跳后落回刺上。无敌帧会保护穿过相邻危险格的过程。
    /// </summary>
    private float ResolveSafeDirection(float preferred)
    {
        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null)
            return preferred;   // 无网格：直接用首选方向

        if (!IsCellBlocked(grid, preferred))
            return preferred;

        if (!IsCellBlocked(grid, -preferred))
            return -preferred;

        return preferred;   // 两侧都堵：仍按首选方向水平逃离，靠无敌穿过，不再原地弹跳
    }

    /// <summary>探测指定方向前方格子是否可通行（复用 NavGrid2D，越界视为不可通行）。</summary>
    private bool IsCellBlocked(NavGrid2D grid, float dir)
    {
        Vector2 ahead = (Vector2)_animal.transform.position + new Vector2(dir * ProbeDistance, 0.2f);
        Vector2Int cell = grid.WorldToCell(ahead);
        return grid.IsBlocked(cell);
    }

    public override void Reset()
    {
        _hasHopped = false;
        _wasGroundedLastFrame = false;
        _startTime = 0f;
    }
}
