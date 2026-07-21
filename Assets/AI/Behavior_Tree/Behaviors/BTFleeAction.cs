using UnityEngine;

/// <summary>
/// [BT] 逃跑节点：智能逃离玩家，综合地形感知、紧迫度升级、玩家预测。
/// 紧急度随连续跳次数递增（1.5x → 2.0x → 2.5x），拉远距离后逐步衰减。
/// 前方遇墙时尝试反向跳跃，遇沟时尝试更远跨越跳跃。
/// 根据玩家速度做简单预判，提前修正逃跑方向。
/// 注意：Reset() 只重置内部状态，绝不调用 StopMoving 等物理副作用。
/// </summary>
public class BTFleeAction : BTNode
{
    private readonly FrogAI _frog;
    private readonly AnimalBase _animal; // 通用基类引用（用于非青蛙动物）

    // ---- 内部状态 ----
    private int _urgencyLevel;          // 紧迫度等级：0=普通 1=慌张 2=恐慌
    private bool _wasGroundedLastFrame;
    private float _nextMoveTime;        // 永不着地动物（如鱼）用的移动冷却计时
    private float _pushUntil;           // 极近距离强推逃生截止时间
    private float _pushDirection;       // 强推时的逃跑方向
    private Vector2 _lastPlayerPos;
    private Vector2 _playerVelocity;

    // ---- 常量 ----
    private const float UrgencyUpDistance = 3.5f;
    private const float UrgencyDecayDistance = 8f;
    private const int UrgencyMaxLevel = 2;
    private const float ContinuousMoveInterval = 0.5f; // 永不着地动物每 0.5s 更新一次逃跑方向
    private const float PushDuration = 1.0f;            // 极近距离强推持续时长
    private const float PushThreshold = 0.3f;           // 玩家和鱼横向差小于此值时触发强推

    /// <summary>青蛙专用构造（使用 PerformHop + 地形感知）。</summary>
    public BTFleeAction(FrogAI frog)
    {
        _frog = frog;
        _animal = frog;
    }

    /// <summary>通用动物构造（使用 PerformMove，无地形感知）。</summary>
    public BTFleeAction(AnimalBase animal)
    {
        _animal = animal;
    }

    /// <summary>当前紧迫度等级（0=普通，1=慌张，2=恐慌）。仅用于调试。</summary>
    public int UrgencyLevel => _urgencyLevel;

    /// <summary>估算的玩家速度。仅用于调试。</summary>
    public Vector2 PlayerVelocity => _playerVelocity;

    public override State Tick()
    {
        // ---- 追踪玩家速度 ----
        UpdatePlayerVelocity();

        // ---- 逃脱评估 ----
        if (!_animal.IsPlayerDetected || _animal.PlayerDistance > _animal.FleeSafeDistance)
        {
            _animal.StopMoving();
            _animal.PlayAnimation("Idle");
            ResetInternalState();
            return State.Success;
        }

        // ---- 极近距离强推：防止鱼卡在玩家正上方原地抖 ----
        // 玩家和鱼横向差极小时，方向判断 Sign(toFlee.x) 会变成 0，导致不移动
        // 此时先强制朝玩家反方向推一段距离，脱离死区
        float horizontalGap = _animal.PlayerDirection.x;
        if (Mathf.Abs(horizontalGap) < PushThreshold && Time.time >= _pushUntil)
        {
            _pushDirection = -Mathf.Sign(horizontalGap) == 0 ? -1f : -Mathf.Sign(horizontalGap);
            _pushUntil = Time.time + PushDuration;
        }

        if (Time.time < _pushUntil)
        {
            // 强推阶段：以基础速度朝固定方向跑，不每 0.5s 换向
            float speedMult = _animal.FleeSpeedMultiplier;
            if (_frog != null)
                _frog.PerformHop(_pushDirection, speedMult, "Flee");
            else
                _animal.PerformMove(_pushDirection, speedMult);

            _wasGroundedLastFrame = _animal.IsGrounded;
            return State.Running;
        }

        // ---- 移动触发 ----
        // 青蛙等跳跃动物：着地瞬间触发一次跳
        bool justLanded = _animal.IsGrounded && !_wasGroundedLastFrame;

        // 鱼等永不着地动物：每 0.5s 定时更新逃跑方向，而不是只触发一次就停住
        bool timerElapsed = _animal.IsGrounded && _wasGroundedLastFrame && Time.time >= _nextMoveTime;

        if (justLanded || timerElapsed)
        {
            EscalateUrgency();
            PerformFleeMove();

            if (timerElapsed)
                _nextMoveTime = Time.time + ContinuousMoveInterval;
        }

        _wasGroundedLastFrame = _animal.IsGrounded;

        return State.Running;
    }

    /// <summary>
    /// 执行一次智能逃跑移动。
    /// 青蛙：计算方向（含预测+地形感知）、速度倍率，调用 PerformHop。
    /// 其他动物：直接朝反方向 PerformMove。
    /// </summary>
    private void PerformFleeMove()
    {
        // 1. 基础逃跑方向（远离玩家）
        float baseFleeDirection = -Mathf.Sign(_animal.PlayerDirection.x);

        // 2. 玩家预测：玩家快速接近时，额外偏转逃跑方向
        float predictedDirection = PredictFleeDirection(baseFleeDirection);

        // 3. 地形感知（仅青蛙）：前方有墙 → 尝试反向跳；前方有沟 → 尝试更远跨越跳
        float finalDirection = _frog != null ? ApplyTerrainAwareness(predictedDirection) : predictedDirection;

        // 4. 计算速度倍率（含紧迫度加成）
        float speedMultiplier = CalculateSpeedMultiplier();

        // 5. 执行移动
        if (_frog != null)
            _frog.PerformHop(finalDirection, speedMultiplier, "Flee");
        else
            _animal.PerformMove(finalDirection, speedMultiplier);
    }

    /// <summary>
    /// 玩家预测：根据玩家水平速度预判其接近趋势，修正逃跑方向。
    /// 玩家向自己快速移动时，额外偏转 30% 方向权重，避免被堵截。
    /// </summary>
    private float PredictFleeDirection(float baseDirection)
    {
        if (Mathf.Abs(_playerVelocity.x) < 0.5f)
            return baseDirection;

        bool playerChasing = (_animal.PlayerDirection.x > 0 && _playerVelocity.x < 0)
                          || (_animal.PlayerDirection.x < 0 && _playerVelocity.x > 0);

        if (!playerChasing)
            return baseDirection;

        // 玩家速度越快，偏转越明显（最多偏转 30%）
        float chaseIntensity = Mathf.Clamp01(Mathf.Abs(_playerVelocity.x) / 5f);
        float deviation = 0.3f * chaseIntensity;

        // 偏转方向：偏向玩家运动方向的反方向（让玩家更难以预测逃跑路线）
        float deviationDirection = -Mathf.Sign(_playerVelocity.x);

        // 混合基础方向和偏转方向
        float blended = baseDirection * (1f - deviation) + deviationDirection * deviation;
        return blended;
    }

    /// <summary>
    /// 地形感知：利用 EnvironmentMonitor 的地形检测结果调整逃跑方向。
    /// 前方有墙 → 尝试反向跳；前方有沟 → 尝试更远跨越跳（速度×1.3）。
    /// </summary>
    private float ApplyTerrainAwareness(float direction)
    {
        if (_frog.Monitor == null)
            return direction;

        // 前方有墙 → 尝试反向跳（换个方向逃跑）
        if (_frog.Monitor.IsWallAhead)
        {
            return -direction;
        }

        // 前方有沟 → 不做方向改变，但可以在这里增加跳跃力度（由 CalculateSpeedMultiplier 统一处理）
        // 目前保持方向不变，返回原方向
        return direction;
    }

    /// <summary>
    /// 根据紧迫度等级计算最终速度倍率。
    /// 基础倍率 × 紧迫度加成（1.0x / 1.33x / 1.67x）。
    /// 青蛙前方有沟时额外 ×1.3 尝试跨越。
    /// </summary>
    private float CalculateSpeedMultiplier()
    {
        // 基础倍率：1.5 → 2.0 → 2.5
        float[] urgencyMultipliers = { 1f, 1.33f, 1.67f };
        float urgencyBonus = urgencyMultipliers[Mathf.Clamp(_urgencyLevel, 0, UrgencyMaxLevel)];

        // 前方有沟 → 额外 1.3 倍尝试跨越（仅青蛙）
        float gapBonus = (_frog != null && _frog.Monitor != null && _frog.Monitor.IsGapAhead) ? 1.3f : 1f;

        return _animal.FleeSpeedMultiplier * urgencyBonus * gapBonus;
    }

    /// <summary>
    /// 着地瞬间根据玩家距离升级紧迫度。
    /// 距离 < 3.5 → 提升一级（最高 2 级），模拟"越追越慌"。
    /// 距离 > 8.0 → 降低一级，模拟"拉开距离后冷静下来"。
    /// </summary>
    private void EscalateUrgency()
    {
        if (_animal.PlayerDistance < UrgencyUpDistance)
        {
            _urgencyLevel = Mathf.Min(_urgencyLevel + 1, UrgencyMaxLevel);
        }
        else if (_animal.PlayerDistance > UrgencyDecayDistance)
        {
            _urgencyLevel = Mathf.Max(_urgencyLevel - 1, 0);
        }
    }

    /// <summary>
    /// 通过两帧位置差估算玩家速度（仅在玩家被检测到时有效）。
    /// </summary>
    private void UpdatePlayerVelocity()
    {
        if (!_animal.IsPlayerDetected)
        {
            _playerVelocity = Vector2.zero;
            return;
        }

        Vector2 currentPlayerPos = _animal.transform.position + (Vector3)_animal.PlayerDirection * _animal.PlayerDistance;

        if (Time.frameCount > 1)
        {
            _playerVelocity = (currentPlayerPos - _lastPlayerPos) / Time.deltaTime;
        }

        _lastPlayerPos = currentPlayerPos;
    }

    /// <summary>
    /// 重置内部状态（逃脱成功或被更高优先级打断时调用）。
    /// 只做纯状态清理，绝不调用 StopMoving 等物理副作用。
    /// </summary>
    private void ResetInternalState()
    {
        _urgencyLevel = 0;
        _wasGroundedLastFrame = false;
        _nextMoveTime = 0f;
        _playerVelocity = Vector2.zero;
        _lastPlayerPos = Vector2.zero;
    }
}
