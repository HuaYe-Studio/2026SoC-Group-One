using UnityEngine;

/// <summary>
/// [BT] 喘息节点（青蛙连跳组间使用）：连跳一组后的短暂停顿，播放 Rest 动画。
/// 与 BTRestAction 的区别：写入 Blackboard.IsPanting 标记，供调试/其他系统区分
/// "觅食节奏中的喘息"与"吞噬眩晕(IsStunned)"——两者互斥。
/// 时长在 [durationMin, durationMax] 间随机，时间到返回 Success。
/// 注意：Reset() 只重置状态并把 IsPanting 复位，不调用物理副作用。
/// </summary>
public class BTPantAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _durationMin;
    private readonly float _durationMax;

    private bool _isPanting;
    private float _pantEndTime;

    public BTPantAction(AnimalBase animal, float durationMin = 0.8f, float durationMax = 1.5f)
    {
        _animal = animal;
        _durationMin = durationMin;
        _durationMax = durationMax;
    }

    public override State Tick()
    {
        // 首帧进入：必须先落地才开始喘息计时，空中保持等待
        if (!_isPanting)
        {
            if (!_animal.IsGrounded)
                return State.Running;

            _animal.StopMoving();
            _animal.PlayAnimation(AnimalAnimNames.Rest);
            _animal.Board.IsPanting = true;
            _pantEndTime = Time.time + Random.Range(_durationMin, _durationMax);
            _isPanting = true;
        }

        if (Time.time >= _pantEndTime)
        {
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    public override void Reset()
    {
        if (_animal != null && _animal.Board != null)
            _animal.Board.IsPanting = false;
        _isPanting = false;
    }
}
