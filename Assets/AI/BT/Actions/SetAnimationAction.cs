/// <summary>
/// [BT] 动画控制动作：调用 AnimalBase.PlayAnimation 切换动画。
/// </summary>
public class SetAnimationAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly string _animName;

    public SetAnimationAction(AnimalBase animal, string animName)
    {
        _animal = animal;
        _animName = animName;
    }

    public override State Tick()
    {
        _animal.PlayAnimation(_animName);
        return State.Success;
    }
}
