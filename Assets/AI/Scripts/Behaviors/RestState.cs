using UnityEngine;

/// <summary>
/// 休息状态：青蛙原地停留较长时间，模拟"歇息"行为。
/// 超时后切换回觅食状态；若检测到玩家则切换至逃跑状态。
/// </summary>
public class RestState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    private float _restEndTime;
    private const float RestDurationMin = 3f;
    private const float RestDurationMax = 6f;

    public RestState(FSM fsm, AnimalBase animal)
    {
        _fsm = fsm;
        _animal = animal;
    }

    public void OnEnter()
    {
        _animal.StopMoving();

        float duration = Random.Range(RestDurationMin, RestDurationMax);
        _restEndTime = Time.time + duration;
    }

    public void OnUpdate()
    {
        if (_animal.IsPlayerDetected)
        {
            _fsm.ChangeState<FleeState>();
            return;
        }

        if (Time.time >= _restEndTime)
        {
            _fsm.ChangeState<ForageState>();
        }
    }

    public void OnExit()
    {
        // 休息状态没有需要清理的资源
    }
}
