using UnityEngine;

/// <summary>
/// 网网蛛AI：继承 AnimalBase，8向爬行（水平地面 + 垂直墙面）。
/// 追捕时朝玩家的完整方向（含垂直）移动，模拟蜘蛛贴墙追逐。
/// 数值初步沿用现有 AI（基础速度 2.0、检测半径 4.5），追捕倍率为初步设定。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnvironmentMonitor))]
public class SpiderAI : AnimalBase
{
    [Header("Climb")]
    [Tooltip("垂直爬行速度倍率（相对水平速度，越低爬墙越慢）")]
    [SerializeField] private float _verticalSpeedMultiplier = 0.8f;

    [Tooltip("追捕玩家速度倍率")]
    [SerializeField] private float _chaseSpeedMultiplier = 1.2f;

    [Tooltip("放弃追捕的距离（米）：玩家跑出此距离停止追击")]
    [SerializeField] private float _abandonChaseDistance = 12f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：Int 枚举，0=Idle 1=Walk 2=Chase 3=Flee 4=Prey
    private const string AnimStateParam = "SPIDER_AnimState";

    public float ChaseSpeedMultiplier => _chaseSpeedMultiplier;
    public float AbandonChaseDistance => _abandonChaseDistance;

    // 覆写基类食物属性，数据来源于 Blackboard（由 EnvironmentMonitor 写入）
    public override bool IsFoodDetected => Board.IsFoodDetected;
    public override Vector2 FoodDirection => Board.FoodDirection;
    public override float FoodDistance => Board.FoodDistance;

    protected override void Awake()
    {
        if (_animator != null)
            Animator = _animator;

        base.Awake();

        // 蜘蛛可贴墙爬行：大幅降低重力，垂直移动由速度直接驱动
        Rb.gravityScale = 0.2f;
    }

    /// <summary>
    /// 8向移动：水平 + 垂直。direction 为完整方向向量（无需归一化，内部取用符号与量级）。
    /// </summary>
    public void Move8(Vector2 direction, float speedMultiplier = 1f)
    {
        NotifyMoveCommand();

        Rb.velocity = new Vector2(
            direction.x * MoveSpeed * speedMultiplier,
            direction.y * MoveSpeed * speedMultiplier * _verticalSpeedMultiplier);

        if (SpriteRenderer != null && Mathf.Abs(direction.x) > 0.05f)
            SpriteRenderer.flipX = direction.x < 0;
    }

    /// <summary>
    /// 追捕玩家：朝玩家的完整方向（含垂直）移动并播放追捕动画。
    /// </summary>
    public void ChasePlayer(float speedMultiplier = 1f)
    {
        Move8(Board.PlayerDirection, speedMultiplier);
        PlayAnimation(AnimalAnimNames.Chase);
    }

    /// <summary>
    /// 平时移动：水平爬行。
    /// </summary>
    public override void PerformMove(float direction, float speedMultiplier = 1f)
    {
        Move8(new Vector2(direction, 0f), speedMultiplier);
        PlayAnimation(AnimalAnimNames.Walk);
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
            case AnimalAnimNames.Walk: state = 1; break;
            case AnimalAnimNames.Chase: state = 2; break;
            case AnimalAnimNames.Flee: state = 3; break;
            case AnimalAnimNames.Prey: state = 4; break;
        }

        _animator.SetInteger(AnimStateParam, state);
    }
}
