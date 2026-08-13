/// <summary>
/// 动物动画状态名常量：集中管理各 AI 调用的动画名，避免魔法字符串散落。
/// 各动物 PlayAnimation 的 switch 与 BT 节点调用处统一引用本类。
/// </summary>
public static class AnimalAnimNames
{
    public const string Idle = "Idle";
    public const string Flee = "Flee";
    public const string Stunned = "Stunned";
    public const string Rest = "Rest";
    public const string Walk = "Walk";
    public const string Chase = "Chase";
    public const string Charge = "Charge";
    public const string Jump = "Jump";
    public const string Prey = "Prey";

    // 泡泡鱼游泳方向态（独立 SwimState 参数）
    public const string SwimForward = "SwimForward";
    public const string SwimUp = "SwimUp";
    public const string SwimDown = "SwimDown";
    public const string Expanded = "Expanded";
}
