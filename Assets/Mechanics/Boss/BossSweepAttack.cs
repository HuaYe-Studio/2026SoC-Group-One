using System.Collections;
using UnityEngine;

/// <summary>
/// [BOSS 攻击 B] 横扫：全屏覆盖攻击。
/// - followDuration=0：无跟随，直接锁定蛇尾出场点展开全屏红框
/// - 命中点取玩家位置（全屏必中）；玩家缺失回退蛇尾锚点
/// - canDestroyHive=false：横扫不破坏蜂巢（BossController 按 CanDestroyHive 跳过蜂巢结算）
/// 概率 20%，与 A 拍击 / C 撕咬按权重归一化调度。
/// </summary>
public class BossSweepAttack : BossAttack
{
    private const float BlinkFrequency = 4f; // 锁定阶段红框闪烁频率（Hz），全屏闪慢一点便于辨识

    private void Awake()
    {
        // 文档固定规格：B 横扫 = 全屏 / 无跟随 / 不破蜂巢 / 概率20
        probability = 20f;
        hitboxSize = new Vector2(16f, 10f); // 全屏覆盖
        canDestroyHive = false;
        followDuration = 0f;
        lockDuration = 0.8f;
    }

    public override IEnumerator Execute(BossAttackContext ctx)
    {
        BossTelegraph telegraph = ctx.telegraph;

        // 命中锚点：玩家位置（全屏攻击必中）；玩家缺失回退蛇尾
        Vector2 hitPoint = ctx.player != null
            ? (Vector2)ctx.player.position
            : (ctx.snakeTail != null ? (Vector2)ctx.snakeTail.position : (Vector2)transform.position);

        // 1. 无跟随：红框在蛇尾出场点展开全屏尺寸
        if (telegraph != null)
        {
            telegraph.BoxSize = HitboxSize;
            telegraph.ShowLock(ctx.snakeTail != null ? ctx.snakeTail.position : (Vector3)hitPoint);
        }

        // 2. 锁定闪烁：最后 0.1s 常亮提示即将命中
        float lockEnd = Time.time + LockDuration;
        while (Time.time < lockEnd)
        {
            if (telegraph != null)
            {
                float remaining = lockEnd - Time.time;
                bool visible = remaining < 0.1f ||
                    Mathf.FloorToInt(Time.time * BlinkFrequency) % 2 == 0;
                telegraph.SetVisible(visible);
            }
            yield return null;
        }

        if (telegraph != null)
            telegraph.SetVisible(true);

        // 3. 命中：BossController 统一结算（全屏 → 玩家必中；蜂巢因 CanDestroyHive=false 跳过）
        ctx.onHit?.Invoke(hitPoint);

        if (telegraph != null)
            telegraph.Hide();
    }
}
