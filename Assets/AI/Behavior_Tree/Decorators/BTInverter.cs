/// <summary>
/// [BT] 反转装饰节点：将子节点的 Success 与 Failure 互换，Running 保持不变。
/// 常用于把"检测到玩家"反转成"未检测到玩家"等条件复用。
/// </summary>
public class BTInverter : BTNode
{
    private readonly BTNode _child;

    public BTInverter(BTNode child)
    {
        _child = child;
    }

    public override State Tick()
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
