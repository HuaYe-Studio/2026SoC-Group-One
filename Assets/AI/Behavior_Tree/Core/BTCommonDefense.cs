using UnityEngine;

/// <summary>
/// [BT] 公共防御子树（阶段 5.1）：抽取 4 棵树共有的"眩晕 → 脱困 → 受伤反馈"三段序列，
/// 按优先级顺序组装为子树 Selector，任意树以 SubTree 引用即可复用（改一处全生效）。
/// 三个分支：
/// 1. 被吞噬/受击眩晕中 → 原地僵直（最高优先级）
/// 2. 卡死 → 脱困（仅次于眩晕）
/// 3. 受伤反馈 → 弹跳 + 位移（眩晕/脱困之后，受伤瞬间抢占）
/// </summary>
public static class BTCommonDefense
{
    /// <summary>构建公共防御子树（根为 Selector，含三个防御分支）。</summary>
    public static BTNode Build(AnimalBase animal, AnimalHurtFeedback feedback)
    {
        if (animal == null)
        {
            Debug.LogError("[BTCommonDefense] animal 为空，无法构建公共防御子树");
            return new BTSelector();
        }

        Blackboard bb = animal.Board;

        BTNode stunnedBranch = new BTSequence(
            new BTCondition(() => bb.IsStunned),
            new BTStunnedAction(animal));

        BTNode unstickBranch = new BTSequence(
            new BTCondition(() => animal.IsStuck),
            new BTUnstickAction(animal));

        BTNode hurtBranch = new BTSequence(
            new BTCondition(() => feedback != null && feedback.IsHurting),
            new BTHurtFeedbackAction(animal, feedback));

        return new BTSelector(stunnedBranch, unstickBranch, hurtBranch);
    }
}
