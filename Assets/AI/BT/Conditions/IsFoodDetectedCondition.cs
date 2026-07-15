/// <summary>
/// [BT] 食物检测条件：检查 AnimalBase.IsFoodDetected。
/// </summary>
public class IsFoodDetectedCondition : BTNode
{
    private readonly AnimalBase _animal;

    public IsFoodDetectedCondition(AnimalBase animal)
    {
        _animal = animal;
    }

    public override State Tick()
    {
        return _animal.IsFoodDetected ? State.Success : State.Failure;
    }
}
