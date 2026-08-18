using System.Collections;
using UnityEngine;

/// <summary>
/// [BOSS 攻击 A] 拍击：红框跟随玩家（followDuration）→ 锁定闪烁（lockDuration）→ 拍下命中。
/// 命中点 = 锁定时玩家位置（玩家不可用时回退蛇尾锚点）。
/// 命中统一走 ctx.onHit(hitPoint) 回调，由 BossController 结算玩家伤害 + 蜂巢破坏，互不耽误。
/// 本类是最小可用攻击实现，作为 A/B/C 三攻击的参照样板：挂到 Boss 子物体后由 BossController 自动发现并调度。
/// </summary>
public class BossSlamAttack : BossAttack
{
    private const float BlinkFrequency = 5f; // 锁定阶段红框闪烁频率（Hz）

    public override IEnumerator Execute(BossAttackContext ctx)
    {
        BossTelegraph telegraph = ctx.telegraph;

        // 1. 跟随预警：红框跟随玩家，给玩家反应窗口
        if (telegraph != null)
        {
            telegraph.BoxSize = HitboxSize;
            telegraph.ShowFollow(ctx.player);
        }

        float followEnd = Time.time + FollowDuration;
        while (Time.time < followEnd)
        {
            if (telegraph != null && ctx.player != null)
                telegraph.FollowTo(ctx.player.position);
            yield return null;
        }

        // 2. 锁定命中点：优先玩家当前位置，玩家缺失时回退蛇尾锚点
        Vector2 hitPoint = ctx.player != null
            ? (Vector2)ctx.player.position
            : (ctx.snakeTail != null ? (Vector2)ctx.snakeTail.position : (Vector2)transform.position);

        if (telegraph != null)
            telegraph.ShowLock(hitPoint);

        // 3. 锁定闪烁：红框在命中点闪烁，最后 0.1s 常亮提示即将命中
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
