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

    // ---- 内部状态 ----
    private int _urgencyLevel;          // 紧迫度等级：0=普通 1=慌张 2=恐慌
    private bool _wasGroundedLastFrame;
    private Vector2 _lastPlayerPos;
    private Vector2 _playerVelocity;    // 通过两帧位置差估算的玩家速度

    // ---- 常量 ----
    private const float UrgencyUpDistance = 3.5f;    // 玩家在此距离内每次落地都会提升紧迫度
    private const float UrgencyDecayDistance = 8f;    // 玩家超过此距离后逐步降低紧迫度
    private const int UrgencyMaxLevel = 2;            // 最高恐慌等级

    public BTFleeAction(FrogAI frog)
    {
        _frog = frog;
    }

    /// <summary>当前紧迫度等级（0=普通，1=慌张，2=恐慌）。仅用于调试。</summary>
    public int UrgencyLevel => _urgencyLevel;

    /// <summary>估算的玩家速度。仅用于调试。</summary>
    public Vector2 PlayerVelocity => _playerVelocity;

    public override State Tick()
    {
        // ---- 追踪玩家速度（无论是否被检测到都更新） ----
        UpdatePlayerVelocity();

        // ---- 逃脱评估 ----
        if (!_frog.IsPlayerDetected || _frog.PlayerDistance > _frog.FleeSafeDistance)
        {
            _frog.StopMoving();
            _frog.PlayAnimation("Idle");
            ResetInternalState();
            return State.Success;
        }

        // ---- 着地瞬间：提升紧迫度 + 起跳 ----
        if (_frog.IsGrounded && !_wasGroundedLastFrame)
        {
            EscalateUrgency();
            PerformFleeHop();
        }
        _wasGroundedLastFrame = _frog.IsGrounded;

        return State.Running;
    }

    /// <summary>
    /// 执行一次智能逃跑跳跃。
    /// 计算方向（含预测+地形感知）、速度倍率（含紧迫度加成）、播放 Flee 动画。
    /// </summary>
    private void PerformFleeHop()
    {
        // 1. 基础逃跑方向（远离玩家）
        float baseFleeDirection = -Mathf.Sign(_frog.PlayerDirection.x);

        // 2. 玩家预测：玩家快速接近时，额外偏转逃跑方向
        float predictedDirection = PredictFleeDirection(baseFleeDirection);

        // 3. 地形感知：前方有墙 → 尝试反向跳；前方有沟 → 尝试更远跨越跳
        float finalDirection = ApplyTerrainAwareness(predictedDirection);

        // 4. 计算速度倍率（含紧迫度加成）
        float speedMultiplier = CalculateSpeedMultiplier();

        // 5. 起跳
        _frog.PerformHop(finalDirection, speedMultiplier, "Flee");
    }

    /// <summary>
    /// 玩家预测：根据玩家水平速度预判其接近趋势，修正逃跑方向。
    /// 玩家向自己快速移动时，额外偏转 30% 方向权重，避免被堵截。
    /// </summary>
    private float PredictFleeDirection(float baseDirection)
    {
        if (Mathf.Abs(_playerVelocity.x) < 0.5f)
            return baseDirection;

        // 玩家朝自己移动时（PlayerDirection.x > 0 表示玩家在右侧，玩家 velocity.x < 0 表示玩家向左移动即朝自己）
        bool playerChasing = (_frog.PlayerDirection.x > 0 && _playerVelocity.x < 0)
                          || (_frog.PlayerDirection.x < 0 && _playerVelocity.x > 0);

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
    /// 根据紧迫度等级计算最终跳跃速度倍率。
    /// 基础倍率 × 紧迫度加成（1.0x / 1.33x / 1.67x）。
    /// 前方有沟时额外 ×1.3 尝试跨越。
    /// </summary>
    private float CalculateSpeedMultiplier()
    {
        // 基础倍率：1.5 → 2.0 → 2.5
        float[] urgencyMultipliers = { 1f, 1.33f, 1.67f };
        float urgencyBonus = urgencyMultipliers[Mathf.Clamp(_urgencyLevel, 0, UrgencyMaxLevel)];

        // 前方有沟 → 额外 1.3 倍尝试跨越
        float gapBonus = (_frog.Monitor != null && _frog.Monitor.IsGapAhead) ? 1.3f : 1f;

        return _frog.FleeSpeedMultiplier * urgencyBonus * gapBonus;
    }

    /// <summary>
    /// 着地瞬间根据玩家距离升级紧迫度。
    /// 距离 < 3.5 → 提升一级（最高 2 级），模拟"越追越慌"。
    /// 距离 > 8.0 → 降低一级，模拟"拉开距离后冷静下来"。
    /// </summary>
    private void EscalateUrgency()
    {
        if (_frog.PlayerDistance < UrgencyUpDistance)
        {
            _urgencyLevel = Mathf.Min(_urgencyLevel + 1, UrgencyMaxLevel);
        }
        else if (_frog.PlayerDistance > UrgencyDecayDistance)
        {
            _urgencyLevel = Mathf.Max(_urgencyLevel - 1, 0);
        }
    }

    /// <summary>
    /// 通过两帧位置差估算玩家速度（仅在玩家被检测到时有效）。
    /// </summary>
    private void UpdatePlayerVelocity()
    {
        if (!_frog.IsPlayerDetected)
        {
            _playerVelocity = Vector2.zero;
            return;
        }

        Vector2 currentPlayerPos = _frog.transform.position + (Vector3)_frog.PlayerDirection * _frog.PlayerDistance;

        if (Time.frameCount > 1)
        {
            // 只有帧数 > 1 时才能计算差值（防止第一帧除零）
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
        _playerVelocity = Vector2.zero;
        _lastPlayerPos = Vector2.zero;
    }
}
