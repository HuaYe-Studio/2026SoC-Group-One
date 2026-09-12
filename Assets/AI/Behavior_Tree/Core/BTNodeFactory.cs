using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// [BT] 节点工厂（阶段 3）：反射扫描所有 [BTNode("名字")] 标注类，按名字统一创建节点。
///
/// - 3.1 特性标注节点名：BTNodeFactory 启动时（运行时 BeforeSceneLoad + 编辑器加载时）
///       反射扫描程序集，收集 [BTNode] 类名与类型的映射。
/// - 3.2 统一构造器：优先调用 IBTContext 构造器（参数注入）；兼容无参构造器。
/// - 3.3 Create("Flee", ctx) 一行创建任意节点：替代 new BTFleeAction(...) 散落组装。
///
/// 新增节点流程：写类 + [BTNode("名字")] + IBTContext 构造器，树组装走工厂。
/// 逻辑叶子（BTAction/BTCondition 委托节点）不适合数据驱动，由 BTLayoutParser 的 resolver 提供。
/// </summary>
public static class BTNodeFactory
{
    private static readonly Dictionary<string, Type> _registry =
        new Dictionary<string, Type>(StringComparer.Ordinal);
    private static bool _scanned;

    /// <summary>播放启动时扫描一次（数据驱动树在运行时也要创建节点）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ScanOnLoad() => Scan();

#if UNITY_EDITOR
    /// <summary>编辑器加载/域重载时扫描（编辑器工具可直接用工厂）。</summary>
    [UnityEditor.InitializeOnLoadMethod]
    private static void ScanOnEditorLoad() => Scan();
#endif

    /// <summary>反射扫描收集 [BTNode] 类（幂等，可重复调用）。</summary>
    public static void Scan()
    {
        if (_scanned) return;
        _scanned = true;
        _registry.Clear();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException)
            {
                continue; // 跳过加载不完整的程序集（如平台专用）
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || !typeof(BTNode).IsAssignableFrom(type)) continue;
                var attr = type.GetCustomAttribute<BTNodeAttribute>(false);
                if (attr == null || string.IsNullOrEmpty(attr.Name)) continue;

                if (!_registry.ContainsKey(attr.Name))
                    _registry.Add(attr.Name, type);
                else
                    Debug.LogWarning($"[BTNodeFactory] 节点名冲突 '{attr.Name}'：{_registry[attr.Name].Name} 与 {type.Name}");
            }
        }
    }

    /// <summary>已注册的节点名（调试/编辑器菜单用）。</summary>
    public static IReadOnlyCollection<string> RegisteredNames => _registry.Keys;

    /// <summary>按名字创建节点；未注册或缺少可用构造器时返回 null 并告警。</summary>
    public static BTNode Create(string name, IBTContext ctx)
    {
        Scan();
        if (!_registry.TryGetValue(name, out var type))
        {
            Debug.LogWarning($"[BTNodeFactory] 未注册的节点名 '{name}'。已注册：{string.Join(", ", _registry.Keys)}");
            return null;
        }

        // 优先统一构造器：IBTContext（3.2 参数注入）
        var ctxCtor = type.GetConstructor(new[] { typeof(IBTContext) });
        if (ctxCtor != null)
            return (BTNode)ctxCtor.Invoke(new object[] { ctx });

        // 兼容无参构造器（如组合节点 Selector/Sequence）
        var emptyCtor = type.GetConstructor(Type.EmptyTypes);
        if (emptyCtor != null)
            return (BTNode)emptyCtor.Invoke(null);

        Debug.LogWarning($"[BTNodeFactory] 节点 '{name}'（{type.Name}）缺少 IBTContext 或无参构造器，无法实例化");
        return null;
    }
}
