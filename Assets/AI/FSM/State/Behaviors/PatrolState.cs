using UnityEngine;

/// <summary>
/// 巡逻状态：动物在出生点周围的巡逻半径内随机移动。
/// 到达目标点后切换回闲置状态；中途检测到玩家则切换至逃跑状态。
/// </summary>
public class PatrolState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    private Vector2 _targetPosition;
    private float _arrivedThreshold = 0.15f;

    public PatrolState(FSM fsm, AnimalBase animal)
    {
        _fsm = fsm;
        _animal = animal;
    }

    public void OnEnter()
    {
        _targetPosition = GetRandomPatrolPoint();
    }

    public void OnUpdate()
    {
        if (_animal.IsPlayerDetected)
        {
            _fsm.ChangeState<FleeState>();
            return;
        }

        Vector2 toTarget = _targetPosition - (Vector2)_animal.transform.position;
        float distance = toTarget.magnitude;

        if (distance <= _arrivedThreshold)
        {
            _fsm.ChangeState<IdleState>();
            return;
        }

        float direction = Mathf.Sign(toTarget.x);
        _animal.PerformMove(direction);
    }

    public void OnExit()
    {
        _animal.StopMoving();
    }

    /// <summary>
    /// 在巡逻半径内随机选取一个目标点。
    /// </summary>
    private Vector2 GetRandomPatrolPoint()
    {
        Vector2 randomOffset = Random.insideUnitCircle * _animal.PatrolRadius;
        return _animal.SpawnPosition + randomOffset;
    }
}
