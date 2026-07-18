using UnityEngine;

/// <summary>
/// 闲置状态：动物原地停顿随机时长，随后执行由外部传入的超时回调。
/// 如果在闲置期间检测到玩家，立即切换到逃跑状态。
/// </summary>
public class IdleState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;
    private readonly System.Action _onTimeout;
    private readonly System.Action _onIdleTick;
    private readonly System.Action _onSetup;

    private float _idleDuration;
    private float _idleEndTime;

    /// <summary>
    /// </summary>
    /// <param name="fsm">所属状态机</param>
    /// <param name="animal">动物实例</param>
    /// <param name="onTimeout">闲置超时后的行为</param>
    /// <param name="onIdleTick">每帧调用的小动作，可为null</param>
    /// <param name="onSetup">每次进入闲置时调用一次，用于重置状态，可为null</param>
    public IdleState(FSM fsm, AnimalBase animal, System.Action onTimeout,
        System.Action onIdleTick = null, System.Action onSetup = null)
    {
        _fsm = fsm;
        _animal = animal;
        _onTimeout = onTimeout;
        _onIdleTick = onIdleTick;
        _onSetup = onSetup;
    }

    public void OnEnter()
    {
        _animal.StopMoving();
        _animal.PlayAnimation("Idle");
        _onSetup?.Invoke();

        _idleDuration = Random.Range(_animal.PatrolPauseMin, _animal.PatrolPauseMax);
        _idleEndTime = Time.time + _idleDuration;
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

        _onIdleTick?.Invoke();

        // 空中不超时，防止跳起后立即切到觅食状态连跳
        if (_animal.IsGrounded && Time.time >= _idleEndTime)
        {
            _onTimeout?.Invoke();
        }
    }

    public void OnExit()
    {
        // 闲置状态没有需要清理的资源
    }
}
