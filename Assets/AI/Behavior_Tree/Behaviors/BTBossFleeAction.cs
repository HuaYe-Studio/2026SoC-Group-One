using UnityEngine;

/// <summary>
/// [BT] BOSS 逃跑节点：检测到 BOSS 紧迫威胁（Blackboard.IsBossUrgent）时朝远离 BOSS 方向逃跑。
/// 在行为树中置于"逃离玩家"分支之前，兑现"BOSS 本体 > 玩家"的威胁优先级（四档仲裁）。
/// 适配动物：鱼全方向 Swim + Flee 动画；青蛙跳跃 + 地形感知；其余水平 PerformMove。
/// 感知数据统一从 Blackboard 读取（IsBossDetected / BossDirection / BossDistance / IsBossUrgent）。
/// 未检测到 BOSS（感知层未交付/半径外）时返回 Success，自然落到玩家逃跑等后续分支。
/// </summary>
public class BTBossFleeAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly FrogAI _frog;
    private readonly Blackboard _bb;

    private bool _wasGroundedLastFrame;
    private float _nextMoveTime;

    private const float ContinuousMoveInterval = 0.4f; // 永不着地动物（鱼/行走动物）的移动节流

    public BTBossFleeAction(AnimalBase animal)
    {
        _animal = animal;
        _frog = animal as FrogAI;
        _bb = animal.Board;
    }

    public override State Tick()
    {
        // 每帧刷新 BOSS 紧迫状态（迟滞 + 最小持续时长，仲裁逻辑写入）
        _bb.RefreshBossUrgent();

        // 脱离条件：BOSS 不再紧迫 / 已拉开安全距离 → 结束逃跑，回落后续分支
        if (!_bb.IsBossUrgent || !_bb.IsBossDetected || _bb.BossDistance > _animal.FleeSafeDistance)
        {
            _animal.StopMoving();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            Reset();
            return State.Success;
        }

        // 移动触发：跳跃动物（青蛙）着地瞬间起跳一次；鱼/行走动物（IsGrounded 恒 true）按节流定时续移
        bool justLanded = _animal.IsGrounded && !_wasGroundedLastFrame;
        bool timerElapsed = _frog == null && _animal.IsGrounded && _wasGroundedLastFrame && Time.time >= _nextMoveTime;

        if (justLanded || timerElapsed)
        {
            PerformBossFleeMove();
            if (timerElapsed)
                _nextMoveTime = Time.time + ContinuousMoveInterval;
        }

        _wasGroundedLastFrame = _animal.IsGrounded;
        return State.Running;
    }

    private void PerformBossFleeMove()
    {
        // 远离 BOSS（感知层已写入归一化方向）
        Vector2 away = -_bb.BossDirection;

        // BOSS 正上方（away.x≈0）时退化为随机水平方向，避免原地不动
        float direction = Mathf.Abs(away.x) > 0.05f
            ? Mathf.Sign(away.x)
            : (Random.value < 0.5f ? 1f : -1f);

        if (_frog != null)
        {
            direction = ApplyTerrainAwareness(direction);
            _frog.PerformHop(direction, _animal.FleeSpeedMultiplier, AnimalAnimNames.Flee);
        }
        else if (_animal is BubbleFishAI fish)
        {
            fish.Swim(away, _animal.FleeSpeedMultiplier);
            fish.PlayAnimation(AnimalAnimNames.Flee);
        }
        else
        {
            _animal.PerformMove(direction, _animal.FleeSpeedMultiplier);
        }
    }

    /// <summary>地形感知：前方有墙或危险物（尖刺）→ 反向跳，避免撞墙乒乓（仅青蛙）。</summary>
    private float ApplyTerrainAwareness(float direction)
    {
        if (_bb.IsWallAhead || _bb.IsHazardAhead)
            return -direction;
        return direction;
    }

    public override void Reset()
    {
        _wasGroundedLastFrame = false;
        _nextMoveTime = 0f;
    }
}
