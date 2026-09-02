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

    private bool _isPanting;    // 是否处于喘息中（首帧初始化，防止重复计时）
    private float _pantEndTime; // 喘息结束时间点（Time.time）

    /// <param name="animal">动物实例（读取 IsGrounded 与 Board.IsPanting）</param>
    /// <param name="durationMin">喘息最短时长（秒）</param>
    /// <param name="durationMax">喘息最长时长（秒）</param>
    public BTPantAction(AnimalBase animal, float durationMin = 0.8f, float durationMax = 1.5f)
    {
        _animal = animal;
        _durationMin = durationMin;
        _durationMax = durationMax;
    }

    /// <summary>
    /// 喘息过程：首帧必须已着地才开始计时（空中进入保持等待，防止刚起跳就误入喘息），
    /// 着地后停动、播 Rest 动画并写 IsPanting，停顿 [durationMin, durationMax] 秒后返回 Success。
    /// </summary>
    protected override State DoTick()
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

        // 停顿结束 → 复位并完成本次喘息
        if (Time.time >= _pantEndTime)
        {
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    /// <summary>结束喘息：复位 IsPanting 标记与内部状态（喘息完成或行为树打断时调用）。</summary>
    public override void Reset()
    {
        if (_animal != null && _animal.Board != null)
            _animal.Board.IsPanting = false;
        _isPanting = false;
    }
}
