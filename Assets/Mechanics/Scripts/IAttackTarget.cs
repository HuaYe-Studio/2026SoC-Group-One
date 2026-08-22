using System;
using UnityEngine;

/// <summary>
/// 可被攻击目标抽象：蜜蜂等攻击单位只依赖此接口，不直接引用具体类（BossController/动物等）。
/// 任何可被攻击的单位（BOSS、未来其他单位如青蛙）实现本接口，即可被蜜蜂等攻击系统复用——
/// 追踪、造成伤害、受创驱散、被击败驱散 四类行为全部面向接口编程。
///
/// 与 IHazardSource 的区别：IHazardSource 是"伤害源→受伤者"的被动伤害；
/// 本接口是"攻击者→主动目标"的主动攻击契约。
/// </summary>
public interface IAttackTarget
{
    /// <summary>目标当前位置（世界坐标，攻击单位追踪与判定基准）。</summary>
    Vector2 Position { get; }

    /// <summary>目标是否存活/可被攻击（蜜蜂据此在攻击与待机间切换）。</summary>
    bool IsAlive { get; }

    /// <summary>对目标造成固定伤害（结算方式由实现方决定，如 BOSS 段血、普通单位扣血）。</summary>
    void TakeDamage(int damage);

    /// <summary>目标受创到阈值（如 BOSS 段血打空）→ 攻击单位驱散飞离。实现方不触发即永不驱散。</summary>
    event Action OnWeakened;

    /// <summary>目标被击败（死亡）→ 攻击单位驱散飞离。</summary>
    event Action OnDefeated;
}
