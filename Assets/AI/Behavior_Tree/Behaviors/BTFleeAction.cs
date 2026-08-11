using UnityEngine;

/// <summary>
/// [BT] 逃跑节点：智能逃离玩家，综合地形感知、紧迫度升级。
/// 紧急度随连续跳次数递增（1.5x → 2.0x → 2.5x），拉远距离后逐步衰减。
/// 前方遇墙时尝试反向跳跃，遇沟时尝试更远跨越跳跃。
/// 感知数据统一从 Blackboard 读取。
/// 适配动物：青蛙走跳跃+地形感知；泡泡鱼(BubbleFishAI)走全方向 Swim+逃生动画；其余走水平 PerformMove。
/// 注意：Reset() 只重置内部状态，绝不调用 StopMoving 等物理副作用。
/// </summary>
public class BTFleeAction : BTNode
{
    private readonly FrogAI _frog;
    private readonly AnimalBase _animal; // 通用基类引用（用于非青蛙动物）
    private readonly Blackboard _bb;

    // ---- 内部状态 ----
    private int _urgencyLevel;          // 紧迫度等级：0=普通 1=慌张 2=恐慌
    private bool _wasGroundedLastFrame;
    private float _nextMoveTime;        // 永不着地动物（如鱼）用的移动冷却计时
    private float _pushUntil;           // 极近距离强推逃生截止时间
    private float _pushDirection;       // 强推时的逃跑方向
    private int _consecutiveWallHits;   // 连续撞墙次数（防墙角乒乓）
    private float _lastWallHitTime;     // 最近一次撞墙时间戳

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
        _bb = frog.Board;
    }

    /// <summary>通用动物构造（使用 PerformMove，无地形感知）。</summary>
    public BTFleeAction(AnimalBase animal)
    {
        _animal = animal;
        _bb = animal.Board;
    }

    /// <summary>当前紧迫度等级（0=普通，1=慌张，2=恐慌）。仅用于调试。</summary>
    public int UrgencyLevel => _urgencyLevel;

    public override State Tick()
    {
        // ---- 逃脱评估 ----
        if (!_bb.IsPlayerVisible || _bb.PlayerDistance > _animal.FleeSafeDistance)
        {
            _animal.StopMoving();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            ResetInternalState();
            return State.Success;
        }

        // ---- 极近距离强推：防止鱼卡在玩家正上方原地抖 ----
        // 玩家和鱼横向差极小时，方向判断 Sign(toFlee.x) 会变成 0，导致不移动
        // 此时先强制朝玩家反方向推一段距离，脱离死区
        float horizontalGap = _bb.PlayerDirection.x;
        if (Mathf.Abs(horizontalGap) < PushThreshold && Time.time >= _pushUntil)
        {
            _pushDirection = -Mathf.Sign(horizontalGap) == 0 ? -1f : -Mathf.Sign(horizontalGap);
            _pushUntil = Time.time + PushDuration;
        }

        if (Time.time < _pushUntil)
        {
            // 强推阶段：以基础速度朝固定方向跑，不每 0.5s 换向
            // 只在地面执行，防止空中连续施加速度造成"二段跳"
            if (!_animal.IsGrounded)
            {
                _animal.StopMoving();
                return State.Running;
            }
            float speedMult = _animal.FleeSpeedMultiplier;
            if (_frog != null)
                _frog.PerformHop(_pushDirection, speedMult, "Flee");
            else if (_animal is BubbleFishAI fish)
                fish.Swim(new Vector2(_pushDirection, 0f), speedMult); // 鱼：沿强推方向全方向游
            else
                _animal.PerformMove(_pushDirection, speedMult);

            _wasGroundedLastFrame = _animal.IsGrounded;
            return State.Running;
        }

        // ---- 移动触发 ----
        // 青蛙等跳跃动物：着地瞬间触发一次跳
        bool justLanded = _animal.IsGrounded && !_wasGroundedLastFrame;

        // 鱼等永不着地动物：每 0.5s 定时更新逃跑方向，而不是只触发一次就停住
        // 注意：定时路径仅对"永不着地动物"（_frog==null，IsGrounded 恒 true）生效，
        // 防止青蛙落地帧同时命中 justLanded 与 timerElapsed 造成连续两次起跳（紧迫度误翻倍）
        bool timerElapsed = _frog == null
            && _animal.IsGrounded && _wasGroundedLastFrame && Time.time >= _nextMoveTime;

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
    /// 青蛙：计算方向（含地形感知）、速度倍率，调用 PerformHop。
    /// 其他动物：直接朝反方向 PerformMove。
    /// </summary>
    private void PerformFleeMove()
    {
        // 1. 基础逃跑方向（远离玩家）
        float baseFleeDirection = -Mathf.Sign(_bb.PlayerDirection.x);

        // 2. 地形感知（仅青蛙）：前方有墙 → 尝试反向跳
        float finalDirection = _frog != null ? ApplyTerrainAwareness(baseFleeDirection) : baseFleeDirection;

        // 3. 计算速度倍率（含紧迫度加成）
        float speedMultiplier = CalculateSpeedMultiplier();

        // 4. 执行移动（鱼：全方向远离玩家 + Flee 动画；其他动物：水平逃跑）
        if (_frog != null)
            _frog.PerformHop(finalDirection, speedMultiplier, "Flee");
        else if (_animal is BubbleFishAI fish)
        {
            fish.Swim(-_bb.PlayerDirection, speedMultiplier);
            fish.PlayAnimation(AnimalAnimNames.Flee);
        }
        else
            _animal.PerformMove(finalDirection, speedMultiplier);
    }

    /// <summary>
    /// 地形感知：利用 Blackboard 的地形感知结果调整逃跑方向。
    /// 前方有墙 → 第1次反向跳（换方向逃）；连续撞墙 → 原地垂直跳，
    /// 避免"逃离→撞墙→朝玩家方向跳→再撞墙"的墙角乒乓。
    /// </summary>
    private float ApplyTerrainAwareness(float direction)
    {
        if (_frog == null)
            return direction;

        if (_bb.IsWallAhead)
        {
            _consecutiveWallHits++;
            _lastWallHitTime = Time.time;

            // 连续撞墙 ≥2 次：原地垂直跳（方向 0 只加垂直力），先挣脱墙角再做方向选择
            if (_consecutiveWallHits >= 2)
                return 0f;

            return -direction;
        }

        // 一段时间未撞墙 → 重置计数
        if (Time.time - _lastWallHitTime > 0.5f)
            _consecutiveWallHits = 0;

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
        float gapBonus = (_frog != null && _bb.IsGapAhead) ? 1.3f : 1f;

        return _animal.FleeSpeedMultiplier * urgencyBonus * gapBonus;
    }

    /// <summary>
    /// 着地瞬间根据玩家距离升级紧迫度。
    /// 距离 < 3.5 → 提升一级（最高 2 级），模拟"越追越慌"。
    /// 距离 > 8.0 → 降低一级，模拟"拉开距离后冷静下来"。
    /// </summary>
    private void EscalateUrgency()
    {
        if (_bb.PlayerDistance < UrgencyUpDistance)
        {
            _urgencyLevel = Mathf.Min(_urgencyLevel + 1, UrgencyMaxLevel);
        }
        else if (_bb.PlayerDistance > UrgencyDecayDistance)
        {
            _urgencyLevel = Mathf.Max(_urgencyLevel - 1, 0);
        }
    }

    /// <summary>
    /// 被高优先级分支打断或重新进入时由行为树调用。
    /// 必须覆写：否则恐慌等级/墙计数会跨逃跑片段残留，
    /// 导致下次逃跑从"恐慌"起步（B2）。
    /// 只做纯状态清理，绝不调用 StopMoving 等物理副作用。
    /// </summary>
    public override void Reset()
    {
        ResetInternalState();
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
        _consecutiveWallHits = 0;
        _lastWallHitTime = 0f;
    }
}
