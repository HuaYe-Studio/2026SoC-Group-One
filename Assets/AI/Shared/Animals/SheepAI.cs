using UnityEngine;

/// <summary>
/// 冲冲羊AI：继承 AnimalBase，平时行走，玩家进入触发半径时冲撞攻击。
/// 冲撞期间持续施加高倍率水平速度，撞墙/超时/命中玩家后结束并进入冷却。
/// 数值初步沿用现有 AI（基础速度 2.0、检测半径 4.5），冲撞参数为初步设定。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnvironmentMonitor))]
public class SheepAI : AnimalBase
{
    [Header("Charge")]
    [Tooltip("冲撞速度倍率（相对基础移动速度）")]
    [SerializeField] private float _chargeSpeedMultiplier = 2.5f;

    [Tooltip("冲撞持续时长（秒）：超过自动结束")]
    [SerializeField] private float _chargeDuration = 1.2f;

    [Tooltip("冲撞冷却（秒）：一次冲撞结束后多久可再次冲撞")]
    [SerializeField] private float _chargeCooldown = 3f;

    [Tooltip("冲撞触发半径（米）：玩家进入此距离开始冲撞")]
    [SerializeField] private float _chargeTriggerRadius = 3f;

    [Tooltip("冲撞命中半径（米）：玩家进入此距离视为撞到")]
    [SerializeField] private float _chargeHitRadius = 0.8f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    // Animator 参数：Int 枚举，0=Idle 1=Walk 2=Charge 3=Flee 4=Prey
    private const string AnimStateParam = "SHEEP_AnimState";

    private bool _isCharging;
    private float _chargeEndTime;
    private float _chargeDirection;

    public bool IsCharging => _isCharging;
    public float ChargeCooldown => _chargeCooldown;
    public float ChargeTriggerRadius => _chargeTriggerRadius;
    public float ChargeHitRadius => _chargeHitRadius;

    // 覆写基类食物属性，数据来源于 Blackboard（由 EnvironmentMonitor 写入）
    public override bool IsFoodDetected => Board.IsFoodDetected;
    public override Vector2 FoodDirection => Board.FoodDirection;
    public override float FoodDistance => Board.FoodDistance;

    protected override void Awake()
    {
        if (_animator != null)
            Animator = _animator;

        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        UpdateCharge();
    }

    /// <summary>
    /// 开始一次冲撞：朝指定方向施加高倍率水平速度。
    /// </summary>
    /// <param name="direction">冲撞方向（正=右，负=左）</param>
    public void StartCharge(float direction)
    {
        NotifyMoveCommand();

        _isCharging = true;
        _chargeDirection = Mathf.Sign(direction);
        _chargeEndTime = Time.time + _chargeDuration;

        Rb.velocity = new Vector2(_chargeDirection * MoveSpeed * _chargeSpeedMultiplier, Rb.velocity.y);

        if (SpriteRenderer != null)
            SpriteRenderer.flipX = _chargeDirection < 0;

        PlayAnimation("Charge");
    }

    /// <summary>
    /// 主动结束冲撞（命中玩家或节点超时等）。
    /// </summary>
    public void StopCharge()
    {
        if (!_isCharging)
            return;

        _isCharging = false;
        Rb.velocity = new Vector2(0f, Rb.velocity.y);
        PlayAnimation("Idle");
    }

    /// <summary>
    /// 冲撞期间每帧维持速度；超时或前方撞墙则结束冲撞。
    /// </summary>
    private void UpdateCharge()
    {
        if (!_isCharging)
            return;

        if (Time.time >= _chargeEndTime || Board.IsWallAhead)
        {
            StopCharge();
            return;
        }

        // 持续施加冲撞速度，抵消物理摩擦导致的减速
        Rb.velocity = new Vector2(_chargeDirection * MoveSpeed * _chargeSpeedMultiplier, Rb.velocity.y);
    }

    /// <summary>
    /// 平时行走：调用基类水平移动并播放行走动画。冲撞中不受普通移动控制。
    /// </summary>
    public override void PerformMove(float direction, float speedMultiplier = 1f)
    {
        if (_isCharging)
            return;

        base.PerformMove(direction, speedMultiplier);
        PlayAnimation("Walk");
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
            case "Walk": state = 1; break;
            case "Charge": state = 2; break;
            case "Flee": state = 3; break;
            case "Prey": state = 4; break;
        }

        _animator.SetInteger(AnimStateParam, state);
    }
}
