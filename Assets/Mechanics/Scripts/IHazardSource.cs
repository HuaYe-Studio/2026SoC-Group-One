using UnityEngine;

/// <summary>
/// 伤害源抽象：统一"直接死亡源 / 普通伤害源"两类地形/场景危险（尖刺、火焰喷射器等）。
/// 挂载到伤害体上的组件实现此接口（如 ContactDamage），伤害系统据此对玩家与动物统一处理。
/// 动物的受伤反应（弹跳/位移/无敌）由 AnimalHurtFeedback 消费此接口，不在此接口内实现。
/// </summary>
public interface IHazardSource
{
    /// <summary>是否直接死亡源（触碰即死，如岩浆/即死陷阱）。普通伤害源为 false。</summary>
    bool IsInstantKill { get; }

    /// <summary>普通伤害值（非即死源时生效）。</summary>
    int Damage { get; }

    /// <summary>击退力（x=水平，y=垂直），命中后施加给目标。</summary>
    Vector2 Knockback { get; }
}
