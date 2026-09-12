using UnityEngine;

/// <summary>
/// [BT] 脱困节点：检测到动物卡死（AnimalBase.IsStuck）时执行脱困动作。
/// 策略：第1次原地垂直跳，之后左右交替反向跳，最多尝试 MaxAttempts 次。
/// 每次脱困尝试结束后调用 NotifyUnstickAttempt 进入冷却，
/// 避免行为树对同一卡死状态无限重试（防卡死机制的核心出口）。
/// </summary>
public class BTUnstickAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly FrogAI _frog;

    private int _attemptCount;
    private float _nextJumpTime;
    private float _attemptStartTime;
    private bool _hasStarted;

    // 脱困尝试参数
    private const float JumpInterval = 0.7f;       // 两次脱困跳跃间隔（秒）
    private const int MaxAttempts = 3;             // 单轮脱困最大跳跃次数
    private const float MaxAttemptDuration = 3f;   // 单轮脱困总时长上限（秒），超时强制放弃
    private const float UnstickSpeedMultiplier = 1.2f;

    public BTUnstickAction(AnimalBase animal)
    {
        _animal = animal;
        _frog = animal as FrogAI;
    }

    protected override State DoTick()
    {
        // 已不卡死（脱困成功或外部解除）→ 正常结束
        if (!_animal.IsStuck)
        {
            Reset();
            return State.Success;
        }

        // 首次进入本轮脱困：记录起始时间
        if (!_hasStarted)
        {
            _hasStarted = true;
            _attemptStartTime = Time.time;
            _nextJumpTime = Time.time;
        }

        // 超时或超次数兜底：强制放弃，进入脱困冷却
        if (Time.time - _attemptStartTime > MaxAttemptDuration || _attemptCount >= MaxAttempts)
        {
            GiveUp();
            return State.Failure;
        }

        // 着地后按间隔尝试跳跃
        if (Time.time >= _nextJumpTime && _animal.IsGrounded)
        {
            _nextJumpTime = Time.time + JumpInterval;
            _attemptCount++;
            TryJump();
        }

        return State.Running;
    }

    /// <summary>
    /// 执行一次脱困跳跃。
    /// 第1次原地垂直跳（0 方向只加垂直力），之后左右交替，避免卡在角落时原地乒乓。
    /// </summary>
    private void TryJump()
    {
        float direction = _attemptCount == 1 ? 0f : (_attemptCount % 2 == 0 ? -1f : 1f);

        if (_frog != null)
            _frog.PerformHop(direction, UnstickSpeedMultiplier, "Jump");
        else
            _animal.PerformMove(direction, UnstickSpeedMultiplier);
    }

    /// <summary>
    /// 放弃本轮脱困：进入脱困冷却，让行为树回到其他分支。
    /// 冷却结束后若仍卡住，会重新判定并再次尝试。
    /// </summary>
    private void GiveUp()
    {
        Reset();
        _animal.NotifyUnstickAttempt();
    }

    public override void Reset()
    {
        _attemptCount = 0;
        _nextJumpTime = 0f;
        _attemptStartTime = 0f;
        _hasStarted = false;
    }
}
