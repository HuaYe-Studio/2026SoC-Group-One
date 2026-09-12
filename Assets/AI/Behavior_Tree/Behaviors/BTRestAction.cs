using UnityEngine;

/// <summary>
/// [BT] 休息节点：原地停留随机时长（FROG_AnimState=2），时间到返回 Success。
/// 被高优先级分支打断时清除计时，下次重新计时。
/// </summary>
public class BTRestAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _durationMin;
    private readonly float _durationMax;

    private bool _isResting;
    private float _restEndTime;

    public BTRestAction(AnimalBase animal, float durationMin = 3f, float durationMax = 6f)
    {
        _animal = animal;
        _durationMin = durationMin;
        _durationMax = durationMax;
    }

    protected override State DoTick()
    {
        // 首帧进入：必须先落地才开始休息计时，空中保持等待
        if (!_isResting)
        {
            if (!_animal.IsGrounded)
                return State.Running;

            _animal.StopMoving();
            _animal.PlayAnimation(AnimalAnimNames.Rest);
            _restEndTime = Time.time + Random.Range(_durationMin, _durationMax);
            _isResting = true;
        }

        if (Time.time >= _restEndTime)
        {
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _isResting = false;
    }
}
