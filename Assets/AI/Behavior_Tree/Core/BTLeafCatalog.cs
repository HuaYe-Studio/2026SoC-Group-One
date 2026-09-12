using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 逻辑叶子注册源（阶段 E0.4）：name → 叶子构造器（返回 BTNode）。
/// 统一登记"可被通用上下文推导的叶子"（纯 Blackboard 条件等），
/// 供两类使用方共用：
/// - 运行时：BTLayoutParser 的 leafResolver 可先查本目录（BTLeafCatalog.Create），
///   同名等价叶子不必在每棵树的 ResolveLeaf 里重复实现；
/// - 编辑器：BTNodeCatalog 列出 RegisteredNames，作为节点目录"叶子"分组数据源。
///
/// 约定：
/// - 只登记"与所有使用方等价"的通用叶子（如 ThreatUrgent 读 Board.IsThreatUrgent）；
/// - 树特有叶子（依赖实例委托/私有配置，如 ForageBurst、Fish 的 A* 动作）
///   继续留在各树 ResolveLeaf，由树的 leafResolver 按 name 拦截（目录会列出其名）。
/// </summary>
public static class BTLeafCatalog
{
    private static readonly Dictionary<string, Func<IBTContext, BTNode>> _leaves =
        new Dictionary<string, Func<IBTContext, BTNode>>(StringComparer.Ordinal);

    static BTLeafCatalog()
    {
        RegisterDefaults();
    }

    /// <summary>注册叶子（同名覆盖；仅供运行时新增通用叶子调用）。</summary>
    public static void Register(string name, Func<IBTContext, BTNode> builder)
    {
        if (string.IsNullOrEmpty(name) || builder == null) return;
        _leaves[name] = builder;
    }

    /// <summary>按名创建叶子；未注册返回 null。</summary>
    public static BTNode Create(string name, IBTContext ctx)
    {
        if (ctx == null || !_leaves.TryGetValue(name, out var builder))
            return null;
        return builder(ctx);
    }

    /// <summary>已登记的通用叶子名。</summary>
    public static IReadOnlyCollection<string> RegisteredNames => _leaves.Keys;

    /// <summary>是否已登记该叶子名。</summary>
    public static bool IsRegistered(string name) => name != null && _leaves.ContainsKey(name);

    private static void RegisterDefaults()
    {
        // 通用条件叶子：全部只读 Blackboard 语义字段，与各树等价实现一致。
        // 注意：Frog/Spider 的 ShouldSearch 带私有阈值（ThreatLevel >= 30），与 Fish 版不同，
        // 故不登记全局 ShouldSearch，仍由各树 ResolveLeaf 按需处理。
        Register("BossUrgent", ctx =>
            new BTCondition(() =>
            {
                ctx.Board.RefreshBossUrgent();
                return ctx.Board.IsBossUrgent;
            }));
        Register("ThreatUrgent", ctx =>
            new BTCondition(() => ctx.Board.IsThreatUrgent));
        Register("FoodDetected", ctx =>
            new BTCondition(() => ctx.Board.IsFoodDetected));
        Register("ChaseCond", ctx =>
            new BTCondition(() => ctx.Board.IsPlayerVisible && !ctx.Board.IsPlayerSameForm));
    }
}
