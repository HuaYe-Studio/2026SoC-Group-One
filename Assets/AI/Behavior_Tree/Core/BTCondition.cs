using System;

/// <summary>
/// [BT] 条件节点：包装一个布尔判断。
/// 条件为 true 返回 Success，为 false 返回 Failure，不会返回 Running。
/// </summary>
public class BTCondition : BTNode
{
    private readonly Func<bool> _condition;

    public BTCondition(Func<bool> condition)
    {
        _condition = condition;
    }

    protected override State DoTick()
    {
        return _condition() ? State.Success : State.Failure;
    }
}
