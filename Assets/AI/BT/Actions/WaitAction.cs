using UnityEngine;

/// <summary>
/// [BT] 等待动作：挂起指定时长，期间返回 Running。
/// </summary>
public class WaitAction : BTNode
{
    private readonly float _duration;
    private float _startTime;
    private bool _started;

    public WaitAction(float duration)
    {
        _duration = duration;
    }

    public override void OnEnter()
    {
        _started = false;
    }

    public override State Tick()
    {
        if (!_started)
        {
            _startTime = Time.time;
            _started = true;
        }

        return Time.time >= _startTime + _duration ? State.Success : State.Running;
    }
}
