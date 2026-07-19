using UnityEngine;

/// <summary>
/// [BT] 逃跑节点：着地时朝远离玩家的方向加速跳跃逃离（FROG_AnimState=3）。
/// 玩家离开警戒范围或超出安全距离后返回 Success 并切回 Idle 动画。
/// </summary>
public class BTFleeAction : BTNode
{
    private readonly FrogAI _frog;

    public BTFleeAction(FrogAI frog)
    {
        _frog = frog;
    }

    public override State Tick()
    {
        // 脱离危险 → 完成
        if (!_frog.IsPlayerDetected || _frog.PlayerDistance > _frog.FleeSafeDistance)
        {
            _frog.StopMoving();
            _frog.PlayAnimation("Idle");
            return State.Success;
        }

        // 着地时朝远离玩家的方向跳跃，空中不施力
        if (_frog.IsGrounded)
        {
            float fleeDirection = -Mathf.Sign(_frog.PlayerDirection.x);
            _frog.PerformHop(fleeDirection, _frog.FleeSpeedMultiplier, "Flee");
        }

        return State.Running;
    }

    // 注意：不要在 Reset 里调用 StopMoving 等动作副作用。
    // 逃跑分支每帧条件失败都会触发 Reset，副作用会把跳跃的水平速度清零。
}
