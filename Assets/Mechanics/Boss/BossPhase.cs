using System;

/// <summary>
/// BOSS 阶段枚举（5 状态）。阶段越高越狂暴，攻击频率/威胁半径/威胁强度随之提升。
/// Normal=常态；Enrage1~3=三档狂暴；Defeated=已被击败（退场中）。
/// </summary>
public enum BossPhase
{
    Normal = 0,
    Enrage1 = 1,
    Enrage2 = 2,
    Enrage3 = 3,
    Defeated = 4,
}
