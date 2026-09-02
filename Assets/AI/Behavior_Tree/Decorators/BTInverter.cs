using System.Collections.Generic;

/// <summary>
/// [BT] 反转装饰节点：将子节点的 Success 与 Failure 互换，Running 保持不变。
/// 常用于把"检测到玩家"反转成"未检测到玩家"等条件复用。
/// 数据驱动：工厂创建后通过 SetChild 挂唯一子节点。
/// </summary>
[BTNode("Inverter")]
public class BTInverter : BTNode
{
    private BTNode _child;
    private BTNode[] _childrenCache;

    public BTInverter(BTNode child)
    {
        _child = child;
    }

    /// <summary>工厂用：创建空反转节点后挂子节点。</summary>
    public BTInverter() { }

    /// <summary>设置唯一子节点（链式，可覆盖）。</summary>
    public BTInverter SetChild(BTNode child)
    {
        _child = child;
        _childrenCache = null;
        return this;
    }

    public override IReadOnlyList<BTNode> Children => _childrenCache ??= new[] { _child };

    protected override State DoTick()
    {
        State result = _child.Tick();

        switch (result)
        {
            case State.Success:
                return State.Failure;
            case State.Failure:
                return State.Success;
            default:
                return State.Running;
        }
    }

    public override void Reset()
    {
        _child.Reset();
    }
}
