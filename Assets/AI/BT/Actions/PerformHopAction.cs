/// <summary>
/// [BT] 跳跃动作：调用 AnimalBase.PerformMove 执行一次跳跃。
/// 需配合 FrogAI 的覆写实现跳跃式移动。
/// </summary>
public class PerformHopAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly System.Func<float> _directionProvider;
    private readonly float _speedMultiplier;

    /// <param name="animal">动物实例</param>
    /// <param name="directionProvider">方向获取函数（正=右，负=左）</param>
    /// <param name="speedMultiplier">速度倍率</param>
    public PerformHopAction(AnimalBase animal, System.Func<float> directionProvider, float speedMultiplier = 1f)
    {
        _animal = animal;
        _directionProvider = directionProvider;
        _speedMultiplier = speedMultiplier;
    }

    public override State Tick()
    {
        float dir = _directionProvider?.Invoke() ?? 1f;
        _animal.PerformMove(dir, _speedMultiplier);
        return State.Success;
    }
}
