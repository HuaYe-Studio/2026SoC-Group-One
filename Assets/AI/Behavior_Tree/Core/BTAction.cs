using System;

/// <summary>
/// [BT] 动作节点：包装一个返回节点状态的委托，适合简单的单步行为。
/// 需要内部状态的复杂行为请继承 BTNode 单独实现（见 Behaviors 文件夹）。
/// </summary>
public class BTAction : BTNode
{
    private readonly Func<State> _action;

    public BTAction(Func<State> action)
    {
        _action = action;
    }

    public override State Tick()
    {
        return _action();
    }
}
