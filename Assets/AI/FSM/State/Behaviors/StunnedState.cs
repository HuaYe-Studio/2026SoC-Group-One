using UnityEngine;

/// <summary>
/// 眩晕状态：被吞噬/受击后原地僵直，停止移动与感知。
/// 超时后自动切换回闲置状态。
/// </summary>
public class StunnedState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    private float _stunEndTime;
    private readonly float _stunDuration;

    /// <summary>
    /// 创建眩晕状态。
    /// </summary>
    /// <param name="fsm">所属状态机</param>
    /// <param name="animal">动物实例</param>
    /// <param name="stunDuration">眩晕时长（秒）</param>
    public StunnedState(FSM fsm, AnimalBase animal, float stunDuration = 0.5f)
    {
        _fsm = fsm;
        _animal = animal;
        _stunDuration = stunDuration;
    }

    public void OnEnter()
    {
        _animal.StopMoving();
        _animal.PlayAnimation("Stunned");

        _stunEndTime = Time.time + _stunDuration;
    }

    public void OnUpdate()
    {
        // 眩晕期间忽略一切感知（不逃跑、不捕食）
        if (Time.time >= _stunEndTime)
        {
            _fsm.ChangeState<IdleState>();
        }
    }

    public void OnExit()
    {
        // 眩晕结束，无额外清理
    }
}
