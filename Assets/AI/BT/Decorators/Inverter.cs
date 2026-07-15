/// <summary>
/// [BT] 取反装饰器：将子节点 Success→Failure，Failure→Success。
/// 用于 "如果不是 X 就做 Y" 的逻辑。
/// </summary>
public class Inverter : BTNode
{
    private readonly BTNode _child;

    public Inverter(BTNode child)
    {
        _child = child;
    }

    public override void OnEnter()
    {
        _child.OnEnter();
    }

    public override State Tick()
    {
        State childState = _child.Tick();

        return childState switch
        {
            State.Success => State.Failure,
            State.Failure => State.Success,
            _ => State.Running
        };
    }

    public override void OnExit()
    {
        _child.OnExit();
    }
}
