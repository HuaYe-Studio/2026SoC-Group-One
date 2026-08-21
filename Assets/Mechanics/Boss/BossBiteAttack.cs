using System.Collections;
using UnityEngine;

/// <summary>
/// [BOSS 攻击 C] 撕咬：蛇头出击的精准单体攻击。
/// - followDuration=1s：红框跟随玩家（蛇头撕咬预判）
/// - lockDuration=0.5s：锁定玩家当前位置 + 闪烁
/// - 命中点 = 锁定时玩家位置（玩家缺失回退蛇头锚点）
/// - canDestroyHive=true：撕咬可破坏蜂巢
/// 概率 40%，与 A 拍击 / B 横扫按权重归一化调度。
/// </summary>
public class BossBiteAttack : BossAttack
{
    private const float BlinkFrequency = 5f; // 锁定阶段红框闪烁频率（Hz）

    private void Awake()
    {
        // 文档固定规格：C 撕咬 = 2×2 / 跟随+锁定 / 破蜂巢 / 概率40
        probability = 40f;
        hitboxSize = new Vector2(2f, 2f);
        canDestroyHive = true;
        followDuration = 1f;
        lockDuration = 0.5f;
    }

    public override IEnumerator Execute(BossAttackContext ctx)
    {
        BossTelegraph telegraph = ctx.telegraph;

        if (telegraph != null)
            telegraph.BoxSize = HitboxSize;

        // 1. 跟随预警：红框跟随玩家（撕咬预判），给玩家反应窗口（狂暴中跳过跟随，直接锁定）
        if (!ctx.enraged && FollowDuration > 0f)
        {
            if (telegraph != null)
                telegraph.ShowFollow(ctx.player);

            float followEnd = Time.time + FollowDuration;
            while (Time.time < followEnd)
            {
                if (telegraph != null && ctx.player != null)
                    telegraph.FollowTo(ctx.player.position);
                yield return null;
            }
        }

        // 2. 锁定命中点：优先玩家当前位置，玩家缺失时回退蛇头锚点
        Vector2 hitPoint = ctx.player != null
            ? (Vector2)ctx.player.position
            : (ctx.snakeHead != null ? (Vector2)ctx.snakeHead.position : (Vector2)transform.position);

        if (telegraph != null)
            telegraph.ShowLock(hitPoint);

        // 3. 锁定闪烁：最后 0.1s 常亮提示即将命中
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

        // 4. 命中：BossController 统一处理玩家伤害 + 蜂巢破坏
        ctx.onHit?.Invoke(hitPoint);

        if (telegraph != null)
            telegraph.Hide();
    }
}
