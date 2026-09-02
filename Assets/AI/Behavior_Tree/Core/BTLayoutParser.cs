using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] JSON 树加载器（阶段 4）：解析标准树格式 → 查 BTNodeFactory → 递归组装 → 返回根节点。
/// 解析产物为公共可编辑模型 BTEditorNode（E0.1），与 BTLayoutExporter 导出格式双向无损。
///
/// 4.1 JSON 树格式（Unity JsonUtility 兼容，params 用键值对数组表达字典）：
/// {
///   "root": {
///     "type": "Selector",                        // 节点注册名（[BTNode("Selector")]）
///     "name": "根",                              // 可选：节点显示名
///     "params": [ { "key": "range", "value": "6" } ],  // 可选：节点参数表
///     "children": [
///       { "type": "SubTree", "name": "CommonDefense" }, // 5.2 子树引用
///       { "type": "Flee", "name": "逃跑" },
///       { "type": "Wander", "name": "巡游", "params": [ { "key": "range", "value": "8" } ] }
///     ]
///   }
/// }
///
/// 4.2 加载规则（按序解析）：
/// - type="SubTree" → BTSubtreeRegistry 创建子树（阶段 5 模块化复用）；
/// - leafResolver 提供时优先调用（type, name, ctx）→ 逻辑叶子（条件/动作委托）由代码解析，
///   也允许树按名拦截任意叶子（如持有节点引用用于调试）；
/// - 其余 → BTNodeFactory 按 type 创建（[BTNode] 注册节点）；
/// - 递归组装：组合节点 AddChild / 装饰节点 SetChild 挂子树；
/// - nodeDecorator 提供时，对每个已组装完成的节点（含子树）统一包裹（如 BTDebugNode 调试包装）。
///
/// E0.2 双向转换：
/// - ParseLayout(json) → BTEditorNode（编辑器打开树 / 校验用）；
/// - Export（BTLayoutExporter）模型 → JSON 文本，与 ParseLayout 无损往返。
///
/// 用法：
///   TextAsset layout = Resources.Load<TextAsset>("Trees/FrogTree");
///   var root = BTLayoutParser.Load(layout.text, new BTContext(frog), ResolveLeaf);
/// </summary>
public static class BTLayoutParser
{
    /// <summary>JSON 顶层容器：{ "root": {...} }。</summary>
    [Serializable]
    private class LayoutDef { public BTEditorNode root; }

    /// <param name="json">树布局 JSON 文本。</param>
    /// <param name="ctx">节点构造上下文（动物根 + 可选默认参数表）。</param>
    /// <param name="leafResolver">逻辑叶子解析器：(type, name, ctx) → BTNode；返回 null 则回退工厂。</param>
    /// <param name="nodeDecorator">节点装饰器（可选）：每个节点组装完成后调用，如 BTDebugNode 调试包装。</param>
    public static BTNode Load(string json, IBTContext ctx,
        Func<string, string, IBTContext, BTNode> leafResolver = null,
        Func<BTNode, string, BTNode> nodeDecorator = null)
    {
        BTEditorNode rootDef = ParseLayout(json);
        if (rootDef == null) return null;
        return BuildNode(rootDef, ctx, leafResolver, nodeDecorator, 0);
    }

    /// <summary>
    /// 解析 JSON 树为公共可编辑模型（E0.2）。解析失败返回 null 并输出错误。
    /// </summary>
    public static BTEditorNode ParseLayout(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[BTLayoutParser] JSON 为空");
            return null;
        }

        LayoutDef layout;
        try
        {
            layout = JsonUtility.FromJson<LayoutDef>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BTLayoutParser] JSON 解析失败: {e.Message}");
            return null;
        }

        if (layout == null || layout.root == null)
        {
            Debug.LogError("[BTLayoutParser] JSON 缺少 root 节点");
            return null;
        }

        return layout.root;
    }

    private static BTNode BuildNode(BTEditorNode def, IBTContext ctx,
        Func<string, string, IBTContext, BTNode> leafResolver,
        Func<BTNode, string, BTNode> nodeDecorator, int depth)
    {
        if (def == null || string.IsNullOrEmpty(def.type))
        {
            Debug.LogWarning($"[BTLayoutParser] 第 {depth} 层存在无 type 的节点定义");
            return null;
        }

        // 子节点继承父上下文，但节点自带参数表时创建独立上下文（参数不污染兄弟节点）
        IBTContext nodeCtx = ctx;
        if (def.@params != null && def.@params.Count > 0)
            nodeCtx = new BTContext(ctx.Owner, ToDict(def.@params));

        BTNode node = null;

        // 1) 子树引用（5.2）：type="SubTree" 按 name 查 BTSubtreeRegistry
        if (def.type == "SubTree")
            node = BTSubtreeRegistry.Create(def.name, nodeCtx);

        // 2) 逻辑叶子 / 树级拦截：resolver 返回非 null 即采用
        if (node == null && leafResolver != null)
            node = leafResolver(def.type, def.name, nodeCtx);

        // 3) 工厂创建（[BTNode] 注册节点）
        if (node == null)
            node = BTNodeFactory.Create(def.type, nodeCtx);

        if (node == null)
        {
            Debug.LogWarning($"[BTLayoutParser] 无法创建节点 type='{def.type}' name='{def.name}'（子树未注册、resolver 未处理、工厂未注册）");
            return null;
        }

        if (!string.IsNullOrEmpty(def.name))
            node.SetNodeName(def.name);

        // 先挂子树（组合节点加子 / 装饰节点设唯一子），再统一装饰
        if (def.children != null)
        {
            foreach (var childDef in def.children)
            {
                var child = BuildNode(childDef, nodeCtx, leafResolver, nodeDecorator, depth + 1);
                if (child != null)
                    Attach(node, child);
            }
        }

        if (nodeDecorator != null)
            node = nodeDecorator(node, def.name);

        return node;
    }

    /// <summary>按节点类型挂子节点（Selector/Sequence 追加，Inverter 设唯一子）。</summary>
    private static void Attach(BTNode parent, BTNode child)
    {
        if (parent is BTSelector selector) { selector.AddChild(child); return; }
        if (parent is BTSequence sequence) { sequence.AddChild(child); return; }
        if (parent is BTInverter inverter) { inverter.SetChild(child); return; }
        Debug.LogWarning($"[BTLayoutParser] 节点 '{parent.NodeName}'（{parent.GetType().Name}）不支持挂子节点");
    }

    private static Dictionary<string, string> ToDict(List<BTEditorParam> defs)
    {
        var dict = new Dictionary<string, string>(defs.Count);
        foreach (var p in defs)
        {
            if (!string.IsNullOrEmpty(p.key))
                dict[p.key] = p.value ?? string.Empty;
        }
        return dict;
    }
}
