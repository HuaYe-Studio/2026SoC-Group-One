using UnityEngine;

/// <summary>
/// [FSM] 休息状态：原地停留 3~6 秒。超时切回觅食。
/// 检测玩家→逃跑，检测食物→捕食。
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
        _animal.PlayAnimation("Rest");

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

        if (_animal.IsFoodDetected)
        {
            _fsm.ChangeState<PounceState>();
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
