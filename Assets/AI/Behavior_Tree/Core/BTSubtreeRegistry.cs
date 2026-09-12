using System;
using System.Collections.Generic;

/// <summary>
/// [BT] 子树注册表（阶段 5.2）：name → 子树构造器。
/// JSON 树中以 { "type": "SubTree", "name": "CommonDefense" } 引用子树，
/// BTLayoutParser 解析时经本注册表创建子树节点。
/// 新增子树流程：写子树构造器（返回 BTNode）+ 一行 Register，树文件即可引用。
/// </summary>
public static class BTSubtreeRegistry
{
    private static readonly Dictionary<string, Func<IBTContext, BTNode>> _registry =
        new Dictionary<string, Func<IBTContext, BTNode>>(StringComparer.Ordinal);

    static BTSubtreeRegistry()
    {
        // 预注册公共防御子树：眩晕 → 脱困 → 受伤反馈（从上下文取动物与受伤反馈组件）
        Register("CommonDefense", ctx =>
            BTCommonDefense.Build(ctx.Animal, ctx.GetComponent<AnimalHurtFeedback>()));
    }

    /// <summary>注册子树构造器（同名覆盖）。</summary>
    public static void Register(string name, Func<IBTContext, BTNode> builder)
    {
        if (string.IsNullOrEmpty(name) || builder == null) return;
        _registry[name] = builder;
    }

    /// <summary>按名创建子树；未注册返回 null。</summary>
    public static BTNode Create(string name, IBTContext ctx)
    {
        if (ctx == null || !_registry.TryGetValue(name, out var builder))
            return null;
        return builder(ctx);
    }

    /// <summary>已注册的子树名（编辑器目录数据源）。</summary>
    public static IReadOnlyCollection<string> RegisteredNames => _registry.Keys;
}
