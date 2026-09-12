using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 网网蛛行为树：挂载并启用后驱动网网蛛AI。
/// 性格：敌对动物——玩家为异形态时主动追捕攻击；玩家同为蜘蛛形态时视为友好，不追捕。
/// 优先级从高到低：眩晕 > 脱困 > 追捕（玩家可见且非同形态）> 搜索 > 巡游。
/// 感知数据统一从 Blackboard 读取语义化认知状态。
/// </summary>
[RequireComponent(typeof(SpiderAI))]
public class SpiderBT : MonoBehaviour
{
    /// <summary>本组件加载的行为树 JSON 名（与 Resources.Load 共用同一常量，编辑器据此反查绑定）。</summary>
    public static string TreeAssetName => "Spider";

    [Header("Chase")]
    [Tooltip("追捕玩家速度倍率")]
    [SerializeField] private float _chaseSpeedMultiplier = 1.2f;

    [Header("Search")]
    [Tooltip("威胁值高于此值时触发搜索行为")]
    [SerializeField] private float _searchThreatThreshold = 30f;

    [Header("Wander")]
    [Tooltip("自由巡游范围半径（米）")]
    [SerializeField] private float _wanderRange = 5f;

    [Header("群体巡游 (Boids)")]
    [Tooltip("启用 Boids 三力修正漫游方向（聚合/对齐/分离）。\n关闭时完全退化为普通巡游（回退开关）")]
    [SerializeField] private bool _enableBoids = true;

    [Tooltip("邻居查询半径（米）：只与同群（FlockId 相同）的同类结群")]
    [SerializeField] private float _flockNeighborRadius = 5f;

    [Tooltip("分离半径（米）：小于此距离的同类产生排斥（防扎堆）")]
    [SerializeField] private float _flockSeparationRadius = 1.2f;

    [Tooltip("分离力权重（三力中最高，防扎堆优先）")]
    [SerializeField] private float _separationWeight = 0.8f;

    [Tooltip("对齐力权重（朝群体平均游向修正）")]
    [SerializeField] private float _alignmentWeight = 0.4f;

    [Tooltip("聚合权重（朝群体质心修正；过强会群体收缩挤成一团，建议 ≤0.3）")]
    [SerializeField] private float _cohesionWeight = 0.25f;

    [Tooltip("修正强度上限（0~1：三力相对导航方向的最大混合比例，防三力盖过导航）")]
    [SerializeField, Range(0f, 1f)] private float _flockMaxSteer = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private SpiderAI _spider;
    private BTNode _root;
    private AnimalHurtFeedback _hurtFeedback; // 受伤反馈组件（受伤时弹跳+位移）

    // 群体巡游（Boids）：成员标识 + 邻居查询缓冲（复用，非分配）
    private FlockMember _flockMember;
    private readonly List<FlockMember> _neighbors = new List<FlockMember>(16);

    // 领地：个体领地（每只蜘蛛独立领地，限定在蜘蛛网区域内）
    private string _territoryKey;
    private bool _territoryReady;

    // 调试用：只在分支/结果变化时输出日志
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void OnDestroy()
    {
        // 阶段 0.3：注销本棵树（对象销毁时从注册表移除，避免悬空引用）
        BTTreeRegistry.Unregister(this);
    }

    private void Awake()
    {
        _spider = GetComponent<SpiderAI>();
        _flockMember = GetComponent<FlockMember>();

        // Boids 兜底：prefab 未挂 FlockMember 时运行时补挂（Awake 阶段添加，当帧完成群注册）
        if (_flockMember == null && _enableBoids)
            _flockMember = gameObject.AddComponent<FlockMember>();

        // 个体差异：确保存在 AnimalStats（随机基础数值 + 强度分），未挂则运行时补挂
        AnimalStats stats = GetComponent<AnimalStats>();
        if (stats == null)
            stats = gameObject.AddComponent<AnimalStats>();

        // 受伤反馈：确保存在 AnimalHurtFeedback（受伤时弹跳+位移+无敌），未挂则运行时补挂
        _hurtFeedback = GetComponent<AnimalHurtFeedback>();
        if (_hurtFeedback == null)
            _hurtFeedback = gameObject.AddComponent<AnimalHurtFeedback>();

        // MC 式软推开：同类重叠时沿最短穿透轴物理推开；蜘蛛 8 向爬墙 → 2D 全向
        AnimalSoftPush softPush = GetComponent<AnimalSoftPush>();
        if (softPush == null)
            softPush = gameObject.AddComponent<AnimalSoftPush>();
        softPush.Dimension = AnimalSoftPush.PushDimension.Omnidirectional;

        // 注册个体领地：每只蜘蛛独立领地（限定在蜘蛛网区域内），半径随强度分映射
        _territoryKey = gameObject.GetInstanceID().ToString();
        TerritoryManager.Register(_territoryKey, _spider.SpawnPosition, AnimalRegion.RegionType.SpiderWeb, isShared: false, strength: stats.Strength);

        _root = BuildTree();

        // 阶段 0.3：向注册表登记本棵行为树（调试器/可视化据此发现树结构）
        BTTreeRegistry.Register(gameObject.name, _root, this);
    }

    /// <summary>
    /// 从 JSON 资产组装蜘蛛行为树（阶段 4.3 数据驱动）。
    /// 树结构/分支顺序/节点参数走 Assets/Resources/BTTrees/Spider.json；
    /// 逻辑叶子（条件/动作委托）由 ResolveLeaf 按名解析。JSON 缺失或解析失败时回退代码版。
    /// </summary>
    private BTNode BuildTree()
    {
        TextAsset asset = Resources.Load<TextAsset>("BTTrees/" + TreeAssetName);
        if (asset == null)
        {
            Debug.LogError("[SpiderBT] 未找到 JSON 树资产 Resources/BTTrees/Spider，回退代码组装");
            return BuildTreeLegacy();
        }

        BTNode root = BTLayoutParser.Load(asset.text, new BTContext(this), ResolveLeaf);
        if (root == null)
        {
            Debug.LogError("[SpiderBT] JSON 树解析失败，回退代码组装");
            return BuildTreeLegacy();
        }
        return root;
    }

    /// <summary>JSON 逻辑叶子解析器：按 name 返回对应条件/动作节点（结构在 JSON，逻辑委托在代码）。</summary>
    private BTNode ResolveLeaf(string type, string name, IBTContext ctx)
    {
        // 优先查通用叶子目录（ChaseCond 等与本地等价）
        BTNode fromCatalog = BTLeafCatalog.Create(name, ctx);
        if (fromCatalog != null) return fromCatalog;

        Blackboard bb = _spider.Board;
        switch (name)
        {
            case "ChaseCond": return new BTCondition(() => bb.IsPlayerVisible && !bb.IsPlayerSameForm);
            case "ShouldSearch": return new BTCondition(() => bb.ShouldSearch && bb.ThreatLevel >= _searchThreatThreshold);
            case "ChasePlayer": return new BTChasePlayerAction(_spider, _chaseSpeedMultiplier);
            case "Search": return new BTSearchAction(_spider, 1f, 1.2f);
            case "Wander": return new BTWanderAction(_spider, _wanderRange, default, ApplyFlockSteering, GetTerritoryCenter);
            default: return null; // 组合/装饰等结构节点交还工厂
        }
    }

    /// <summary>
    /// 代码组装版（JSON 资产缺失/解析失败时的回退）。
    /// </summary>
    private BTNode BuildTreeLegacy()
    {
        Blackboard bb = _spider.Board;

        // 分支0：被吞噬/受击眩晕中 → 原地僵直（最高优先级）
        BTNode stunnedBranch = new BTSequence(
            new BTCondition(() => bb.IsStunned),
            new BTStunnedAction(_spider));

        // 分支1：卡死 → 脱困（仅次于眩晕）
        BTNode unstickBranch = new BTSequence(
            new BTCondition(() => _spider.IsStuck),
            new BTUnstickAction(_spider));

        // 分支2：受伤反馈 → 弹跳 + 位移（眩晕/脱困之后、追捕之前，受伤瞬间抢占）
        BTNode hurtBranch = new BTSequence(
            new BTCondition(() => _hurtFeedback.IsHurting),
            new BTHurtFeedbackAction(_spider, _hurtFeedback));

        // 分支3：玩家可见且非同形态 → 追捕（敌对：不依赖威胁值；玩家同为蜘蛛形态时友好，不追捕）
        BTNode chaseBranch = new BTSequence(
            new BTCondition(() => bb.IsPlayerVisible && !bb.IsPlayerSameForm),
            new BTChasePlayerAction(_spider, _chaseSpeedMultiplier));

        // 分支3：玩家不可见但有威胁记忆 → 前往最后已知位置搜索
        BTNode searchBranch = new BTSequence(
            new BTCondition(() => bb.ShouldSearch && bb.ThreatLevel >= _searchThreatThreshold),
            new BTSearchAction(_spider, 1f, 1.2f));

        // 分支4：默认自由巡游，方向经 Boids 三力修正、中心为个体领地
        BTNode wanderBranch = new BTWanderAction(_spider, _wanderRange, default, ApplyFlockSteering, GetTerritoryCenter);

        return new BTSelector(stunnedBranch, unstickBranch, hurtBranch, chaseBranch, searchBranch, wanderBranch);
    }

    /// <summary>巡游中心：优先用个体领地中心，未分配时回退出生点。</summary>
    private Vector2 GetTerritoryCenter()
    {
        Territory t = TerritoryManager.Get(_territoryKey);
        return t != null ? t.Center : _spider.SpawnPosition;
    }

    /// <summary>
    /// Boids 三力修正：仅作用于漫游分支，聚合/对齐/分离叠加在漫游导航方向上（只做偏移修正，
    /// 路点导航仍由漫游节点负责）。未挂 FlockMember、功能关闭或无邻居时原样返回。
    /// </summary>
    private float ApplyFlockSteering(float direction)
    {
        if (!_enableBoids || _flockMember == null)
            return direction;

        if (FlockManager.GetNeighbors(_flockMember, _flockNeighborRadius, _neighbors) == 0)
            return direction;

        return BoidsSteering.ApplyHorizontal(direction, _flockMember.transform.position, _flockMember.Velocity,
            _neighbors, _flockSeparationRadius, _separationWeight, _alignmentWeight,
            _cohesionWeight, _flockMaxSteer);
    }

    private void Update()
    {
        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _spider = GetComponent<SpiderAI>();
            _root = BuildTree();
        }

        // 首帧：所有动物 Awake 完成后统一分配领地
        if (!_territoryReady)
        {
            TerritoryManager.EnsureAssigned();
            _territoryReady = true;
        }

        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    private void LogStateChange(BTNode.State result)
    {
        Blackboard bb = _spider.Board;

        string branch = bb.IsStunned ? "Stunned眩晕"
            : _spider.IsStuck ? "Unstick脱困"
            : bb.IsPlayerVisible && !bb.IsPlayerSameForm ? "Chase追捕"
            : bb.ShouldSearch ? "Search搜索"
            : "Wander巡游";

        if (branch == _lastBranch && result == _lastResult)
            return;

        Debug.Log($"{gameObject.name} BT: 分支[{branch}] 结果[{result}] 距玩家[{bb.PlayerDistance:F1}m]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
