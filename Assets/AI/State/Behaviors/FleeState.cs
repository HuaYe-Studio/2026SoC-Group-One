using UnityEngine;

/// <summary>
/// [FSM] 逃跑状态：动物朝远离玩家的方向快速移动。
/// 玩家超出安全距离后切回闲置。
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
        _animal.PlayAnimation("Flee");
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
