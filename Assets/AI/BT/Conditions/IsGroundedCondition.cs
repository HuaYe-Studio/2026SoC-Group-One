/// <summary>
/// [BT] 地面检测条件：检查 AnimalBase.IsGrounded。
/// </summary>
public class IsGroundedCondition : BTNode
{
    private readonly AnimalBase _animal;

    public IsGroundedCondition(AnimalBase animal)
    {
        _animal = animal;
    }

    public override State Tick()
    {
        return _animal.IsGrounded ? State.Success : State.Failure;
    }
}
