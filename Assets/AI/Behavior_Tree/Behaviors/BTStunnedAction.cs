using UnityEngine;

/// <summary>
/// [BT] 眩晕节点：原地僵直一段时间，期间不移动、不感知。
/// 被吞噬/受击后由外部触发，超时后返回 Success。
/// 注意：Reset() 只重置计时，不调用物理副作用。
/// </summary>
public class BTStunnedAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _stunDuration;

    private float _stunEndTime;
    private bool _isStunned;

    /// <param name="animal">动物实例</param>
    /// <param name="stunDuration">眩晕时长（秒）</param>
    public BTStunnedAction(AnimalBase animal, float stunDuration = 0.5f)
    {
        _animal = animal;
        _stunDuration = stunDuration;
    }

    public override State Tick()
    {
        // 首帧进入：记录结束时间，播放眩晕动画
        if (!_isStunned)
        {
            _animal.StopMoving();
            _animal.PlayAnimation("Stunned");
            _stunEndTime = Time.time + _stunDuration;
            _isStunned = true;
            return State.Running;
        }

        // 眩晕结束
        if (Time.time >= _stunEndTime)
        {
            _isStunned = false;
            return State.Success;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _isStunned = false;
    }
}
