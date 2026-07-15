/// <summary>
/// [BT] 玩家检测条件：检查 AnimalBase.IsPlayerDetected。
/// </summary>
public class IsPlayerDetectedCondition : BTNode
{
    private readonly AnimalBase _animal;

    public IsPlayerDetectedCondition(AnimalBase animal)
    {
        _animal = animal;
    }

    public override State Tick()
    {
        return _animal.IsPlayerDetected ? State.Success : State.Failure;
    }
}
