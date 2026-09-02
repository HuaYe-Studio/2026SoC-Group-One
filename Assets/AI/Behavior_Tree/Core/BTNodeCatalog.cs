using System.Collections.Generic;

/// <summary>节点目录分组（编辑器"添加节点"菜单的分组）。</summary>
public enum BTNodeCategory
{
    /// <summary>组合节点：Selector / Sequence。</summary>
    Composite,
    /// <summary>装饰节点：Inverter 等。</summary>
    Decorator,
    /// <summary>工厂注册节点（[BTNode] 标注，如 Flee/ChasePlayer/Wander）。</summary>
    Registered,
    /// <summary>逻辑叶子（条件/动作委托名，BTLeafCatalog 通用叶子 + 各树特有叶子名）。</summary>
    Leaf,
    /// <summary>子树引用（BTSubtreeRegistry 已注册，如 CommonDefense）。</summary>
    SubTree,
}

/// <summary>目录条目。</summary>
public struct BTNodeCatalogEntry
{
    public string Name;
    public BTNodeCategory Category;

    public BTNodeCatalogEntry(string name, BTNodeCategory category)
    {
        Name = name;
        Category = category;
    }
}

/// <summary>
/// [BT] 节点目录（阶段 E0.3）：枚举所有可拖入编辑器的节点，供"添加节点"菜单/搜索面板使用。
/// 分组：
/// - 组合/装饰：Selector/Sequence/Inverter（结构节点，硬编码）；
/// - 注册节点：BTNodeFactory.RegisteredNames（[BTNode] 工厂节点，排除结构节点避免重复）；
/// - 逻辑叶子：BTLeafCatalog 通用叶子 + 4 棵树 ResolveLeaf 的特有叶子名（清单）；
/// - 子树：BTSubtreeRegistry.RegisteredNames（如 CommonDefense）。
/// </summary>
public static class BTNodeCatalog
{
    /// <summary>各树 ResolveLeaf 支持、但属于树特有逻辑（未进 BTLeafCatalog）的叶子名。
    /// 编辑器目录据此展示"可放置的叶子"；放置后运行时由对应树的 leafResolver 解析。</summary>
    private static readonly string[] TreeSpecificLeaves =
    {
        // Frog：搜索（带私有阈值）、捕食、觅食、喘息、BOSS 逃跑
        "ShouldSearch", "Pounce", "ForageBurst", "Pant", "BossFlee",
        // Spider：搜索（带私有阈值）、追捕（带实例速度）、巡游（带 Boids 委托）
        "ChasePlayer", "Search", "Wander",
        // Sheep：复仇冲撞（依赖私有复仇控制器）
        "RevengeCond", "Charge",
        // Fish：A* 逃跑/搜索/巡游（依赖实例目标/代价/动画委托）
        "BossEscape", "Escape",
    };

    private static List<BTNodeCatalogEntry> _entries;
    private static bool _initialized;

    /// <summary>全部目录条目（首次访问时组装，含工厂扫描）。</summary>
    public static IReadOnlyList<BTNodeCatalogEntry> Entries
    {
        get
        {
            if (!_initialized)
            {
                _initialized = true;
                Build();
            }
            return _entries;
        }
    }

    /// <summary>按名字查目录条目（找不到返回 null）。</summary>
    public static BTNodeCatalogEntry? Find(string name)
    {
        foreach (var e in Entries)
        {
            if (e.Name == name) return e;
        }
        return null;
    }

    private static void Build()
    {
        _entries = new List<BTNodeCatalogEntry>(32);

        // 组合 / 装饰
        _entries.Add(new BTNodeCatalogEntry("Selector", BTNodeCategory.Composite));
        _entries.Add(new BTNodeCatalogEntry("Sequence", BTNodeCategory.Composite));
        _entries.Add(new BTNodeCatalogEntry("Inverter", BTNodeCategory.Decorator));

        // 注册节点（排除结构节点，避免与上面重复）
        foreach (var name in BTNodeFactory.RegisteredNames)
        {
            if (name is "Selector" or "Sequence" or "Inverter") continue;
            if (Find(name) == null)
                _entries.Add(new BTNodeCatalogEntry(name, BTNodeCategory.Registered));
        }

        // 逻辑叶子：通用 + 树特有（去重）
        foreach (var name in BTLeafCatalog.RegisteredNames)
        {
            if (Find(name) == null)
                _entries.Add(new BTNodeCatalogEntry(name, BTNodeCategory.Leaf));
        }
        foreach (var name in TreeSpecificLeaves)
        {
            if (Find(name) == null)
                _entries.Add(new BTNodeCatalogEntry(name, BTNodeCategory.Leaf));
        }

        // 子树引用
        foreach (var name in BTSubtreeRegistry.RegisteredNames)
        {
            if (Find(name) == null)
                _entries.Add(new BTNodeCatalogEntry(name, BTNodeCategory.SubTree));
        }
    }
}
