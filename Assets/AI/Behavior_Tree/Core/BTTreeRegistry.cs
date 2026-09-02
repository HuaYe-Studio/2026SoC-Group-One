using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 行为树根注册表（阶段 0.3）：所有 *BT.cs 在 Awake 注册根节点、OnDestroy 注销，
/// 供调试器/编辑器发现场景里所有在跑的行为树（完整结构 + 每节点实时状态）。
/// 以 Owner（MonoBehaviour）为主键：同一棵树重建（热重载/对象池复用）时覆盖旧条目，避免重复注册。
/// </summary>
public static class BTTreeRegistry
{
    /// <summary>一棵已注册行为树的条目信息。</summary>
    public sealed class Entry
    {
        public string Name { get; }
        public BTNode Root { get; }
        public Object Owner { get; }
        public float RegisteredTime { get; }

        public Entry(string name, BTNode root, Object owner)
        {
            Name = name;
            Root = root;
            Owner = owner;
            RegisteredTime = Time.time;
        }
    }

    private static readonly List<Entry> _trees = new List<Entry>(8);

    /// <summary>当前注册的全部行为树（只读）。</summary>
    public static IReadOnlyList<Entry> Trees => _trees;

    /// <summary>当前注册的树数量。</summary>
    public static int Count => _trees.Count;

    /// <summary>
    /// 注册/更新一棵树：同一 Owner 再次注册（如热重载重建树）时覆盖旧条目。
    /// </summary>
    public static void Register(string name, BTNode root, Object owner = null)
    {
        if (root == null) return;

        if (owner != null)
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                if (_trees[i].Owner == owner)
                {
                    _trees[i] = new Entry(name, root, owner);
                    return;
                }
            }
        }

        _trees.Add(new Entry(name, root, owner));
    }

    /// <summary>按 Owner 注销一棵树（OnDestroy 时调用）。</summary>
    public static void Unregister(Object owner)
    {
        if (owner == null) return;
        for (int i = _trees.Count - 1; i >= 0; i--)
        {
            if (_trees[i].Owner == owner)
                _trees.RemoveAt(i);
        }
    }

    /// <summary>按名称查找第一棵匹配的树（同名树存在多棵时返回最早注册的）。</summary>
    public static Entry Find(string name)
    {
        for (int i = 0; i < _trees.Count; i++)
            if (_trees[i].Name == name)
                return _trees[i];
        return null;
    }
}
