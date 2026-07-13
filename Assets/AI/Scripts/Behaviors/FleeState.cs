using UnityEngine;

/// <summary>
/// 逃跑状态：动物朝远离玩家的方向移动，速度比平时更快。
/// 当玩家距离超出安全距离后，切换回闲置状态。
/// </summary>
public class FleeState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    public FleeState(FSM fsm, AnimalBase animal)
    {
        _fsm = fsm;
        _animal = animal;
    }

    public void OnEnter()
    {
        // 逃跑状态使用更快的速度倍数
    }

    public void OnUpdate()
    {
        if (!_animal.IsPlayerDetected || _animal.PlayerDistance > _animal.FleeSafeDistance)
        {
            _fsm.ChangeState<IdleState>();
            return;
        }

        // 朝远离玩家的方向移动
        float fleeDirection = -Mathf.Sign(_animal.PlayerDirection.x);
        _animal.PerformMove(fleeDirection, _animal.FleeSpeedMultiplier);
    }

    public void OnExit()
    {
        _animal.StopMoving();
    }
}
