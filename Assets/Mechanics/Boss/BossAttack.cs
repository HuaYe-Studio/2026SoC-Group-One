using System;
using UnityEngine;

/// <summary>
/// 攻击上下文：把"攻击 ↔ BOSS"解耦。具体攻击实现只依赖此上下文，
/// 不直接引用 BossController，BossController 构造上下文并驱动攻击执行。
/// </summary>
public struct BossAttackContext
{
    /// <summary>玩家 Transform（跟随目标，攻击锁定/瞄准用）。</summary>
    public Transform player;

    /// <summary>蛇头 Transform（撕咬 C 的攻击出场点）。</summary>
    public Transform snakeHead;

    /// <summary>蛇尾 Transform（拍击 A / 横扫 B 的攻击出场点）。</summary>
    public Transform snakeTail;

    /// <summary>红框预警组件（ShowFollow/ShowLock/Hide，由 UI 组实现）。</summary>
    public BossTelegraph telegraph;

    /// <summary>命中判定回调：攻击命中点由攻击实现自己判定后调用，Boss 侧统一处理玩家伤害+蜂巢破坏。</summary>
    public Action<Vector2> onHit;

    /// <summary>是否狂暴中：true 时红框跳过跟随阶段直接锁定玩家当前位置（文档 3.4 狂暴锁定）。</summary>
    public bool enraged;
}

/// <summary>
/// 攻击基类：三形态攻击（A 拍击 / B 全屏 / C 撕咬）继承实现 Execute。
/// 由 BossController 按概率（A40/B20/C40）与阶段冷却选择并执行。
/// </summary>
public abstract class BossAttack : MonoBehaviour
{
    [Header("攻击基础配置")]
    [Tooltip("该攻击的选择权重（A=40 / B=20 / C=40，与其他攻击按权重归一化）")]
    [SerializeField] protected float probability = 25f;

    [Tooltip("命中判定盒尺寸（A:3×1.5 拍击 / B:全屏 横扫 / C:2×2 撕咬）")]
    [SerializeField] protected Vector2 hitboxSize = new Vector2(3f, 1.5f);

    [Tooltip("该攻击是否可以破坏蜂巢（A✅ B❌ C✅）")]
    [SerializeField] protected bool canDestroyHive = true;

    [Tooltip("跟随预警时长（秒）：红框先跟随目标（B=0 表示锁定型，无跟随）")]
    [SerializeField] protected float followDuration = 1f;

    [Tooltip("锁定预警时长（秒）：红框锁定+闪烁后再命中")]
    [SerializeField] protected float lockDuration = 0.5f;

    [Header("攻击音效")]
    [Tooltip("攻击命中时播放的音效 key（对应 AudioLibrary 的 sfxEntries）。子类 Awake 里设默认值，Inspector 可覆盖")]
    [SerializeField] protected string attackSfxKey = "";

    /// <summary>选择权重（供 BossController 概率归一化）。</summary>
    public float Probability => probability;

    /// <summary>命中判定盒尺寸（供 BossController 红框/调试绘制）。</summary>
    public Vector2 HitboxSize => hitboxSize;

    /// <summary>是否可破坏蜂巢（供 BossController 选择策略参考）。</summary>
    public bool CanDestroyHive => canDestroyHive;

    /// <summary>跟随预警时长（秒）。</summary>
    public float FollowDuration => followDuration;

    /// <summary>锁定预警时长（秒）。</summary>
    public float LockDuration => lockDuration;

    /// <summary>
    /// 执行一次攻击（协程：预警 → 命中判定 → 收尾）。由 BossController 在攻击冷却结束后调用。
    /// 命中判定统一走 ctx.onHit(hitPoint) 回调，Boss 侧负责玩家伤害 + 蜂巢破坏。
    /// </summary>
    public abstract System.Collections.IEnumerator Execute(BossAttackContext ctx);

    /// <summary>播放攻击命中音效（子类在命中时调用）。</summary>
    protected void PlayAttackSfx()
    {
        if (!string.IsNullOrEmpty(attackSfxKey) && AudioManager.HasInstance)
            AudioManager.Instance.PlaySfxByKey(attackSfxKey);
    }
}
