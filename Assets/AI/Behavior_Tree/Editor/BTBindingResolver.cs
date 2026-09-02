#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// [BT] 树 ↔ 动物/组件 绑定解析（需求②，命名约定方案）：
/// 约定：每个 *BT 组件声明 `public static string TreeAssetName`（与其 Resources.Load("BTTrees/...")
/// 共用同一常量）。本工具反射扫描程序集读取该属性，得到「树名 ↔ 加载组件」双向映射——
/// 编辑器看到的绑定与运行时真实加载永远一致（同一处维护）。
///
/// 使用：
/// - LoaderTypesForTree("Frog") → ["FrogBT"]（这棵树由谁加载）；
/// - TreeNameForType("BubbleFishBT") → "Fish"；
/// - IsTreeBound(name) → 是否有运行时组件会加载它（新建树的加载校验 2.3）。
/// </summary>
public static class BTBindingResolver
{
    private static Dictionary<string, string> _treeToType; // 树名 → 组件类型名（简名）
    private static Dictionary<string, string> _typeToTree; // 组件类型名 → 树名
    private static bool _scanned;

    private static void EnsureScanned()
    {
        if (_scanned) return;
        _scanned = true;
        _treeToType = new Dictionary<string, string>(StringComparer.Ordinal);
        _typeToTree = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "UnityEditor" || asm.GetName().Name == "UnityEngine" ||
                asm.GetName().Name.StartsWith("Unity.") || asm.GetName().Name.StartsWith("UnityEditor."))
                continue;
            foreach (var type in GetLoadableTypes(asm))
            {
                if (type == null || !type.IsClass || type.IsAbstract) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(type)) continue; // 只关心场景组件
                var prop = type.GetProperty("TreeAssetName",
                    BindingFlags.Public | BindingFlags.Static);
                if (prop == null || prop.PropertyType != typeof(string)) continue;
                if (!prop.GetMethod.IsStatic || !prop.GetMethod.IsPublic) continue;

                string treeName;
                try { treeName = prop.GetValue(null) as string; }
                catch (Exception) { continue; }
                if (string.IsNullOrEmpty(treeName)) continue;

                _treeToType[treeName] = type.Name;
                _typeToTree[type.Name] = treeName;
            }
        }
    }

    /// <summary>加载某棵树的所有组件类型名（通常 1 个）。树未绑定返回空列表。</summary>
    public static IReadOnlyList<string> LoaderTypesForTree(string treeName)
    {
        EnsureScanned();
        if (string.IsNullOrEmpty(treeName) || !_treeToType.TryGetValue(treeName, out var typeName))
            return Array.Empty<string>();
        return new[] { typeName };
    }

    /// <summary>组件类型名 → 其加载的树名；未知返回 null。</summary>
    public static string TreeNameForType(string typeShortName)
    {
        EnsureScanned();
        if (typeShortName == null || !_typeToTree.TryGetValue(typeShortName, out var tree))
            return null;
        return tree;
    }

    /// <summary>是否有运行时组件加载这棵树（新建树加载校验：绑定才算可用）。</summary>
    public static bool IsTreeBound(string treeName) => LoaderTypesForTree(treeName).Count > 0;

    /// <summary>已注册的全部「树名 → 组件类型名」对（调试/菜单用）。</summary>
    public static IReadOnlyDictionary<string, string> AllBindings
    {
        get
        {
            EnsureScanned();
            return _treeToType;
        }
    }

    /// <summary>强制重新扫描（新写组件后编辑器重编译时调用）。</summary>
    public static void Rescan() { _scanned = false; EnsureScanned(); }

    private static IEnumerable<Type> GetLoadableTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
    }
}
#endif
