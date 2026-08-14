using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 泡泡鱼行为树：挂载并启用后驱动泡泡鱼AI。
/// 优先级：被吞噬眩晕 > 脱困 > 逃跑(A* 到远离玩家的安全点) >
///         搜索(威胁记忆) > 漫游(A* 到食物点/安全点)。
/// 移动系统：A* 寻路（NavGrid2D 水域局部网格）替代原 FishPath 路径系统。
/// - 空气（区域外）极高代价：鱼不会游上岸
/// - 玩家高代价：寻路自动绕开玩家附近，逃跑目标 = 离玩家最远的安全点
/// - 安全点多点 + 择近迟滞：杜绝单点回撤造成的来回抖动
/// 所有感知判断统一从 Blackboard 读取语义化认知状态。
/// </summary>
[RequireComponent(typeof(BubbleFishAI))]
public class BubbleFishBT : MonoBehaviour
{
    [Header("安全点设置")]
    [Tooltip("安全点数量（多点防抖，建议 3~5；仅在未配置手动安全点时生效）")]
    [SerializeField] private int _safePointCount = 4;

    [Tooltip("安全点采样半径（米，以出生点为中心；仅在未配置手动安全点时生效）")]
    [SerializeField] private float _safePointRadius = 15f;

    [Tooltip("安全点间最小间距（米；仅在未配置手动安全点时生效）")]
    [SerializeField] private float _safePointSpacing = 3f;

    [Tooltip("手动安全点（可选）：场景中放置空物体标记关键点，鱼在这些点之间轮换巡游。\n未配置（为空）时自动随机生成。手动点可精确控制鱼的'家'/巡游路线，需保证点在水域内")]
    [SerializeField] private Transform[] _manualSafePoints;

    [Tooltip("巡游节奏：到达安全点后的停留时长（秒），结束后游向下一个点。设为 0 表示不停顿、连续巡游")]
    [SerializeField] private float _wanderDwellTime = 0f;

    [Tooltip("限定区域类型：只在指定区域内采样安全点")]
    [SerializeField] private AnimalRegion.RegionType _regionType = AnimalRegion.RegionType.Water;

    [Header("A* 寻路设置")]
    [Tooltip("路径重算间隔（秒）。玩家移动导致路径过期时按此频率刷新")]
    [SerializeField] private float _repathInterval = 0.8f;

    [Tooltip("到达路径点的判定半径（米）")]
    [SerializeField] private float _arriveRadius = 0.5f;

    [Tooltip("逃跑速度倍率（用鱼的逃生倍率）")]
    [SerializeField] private float _escapeSpeedMultiplier = 1.5f;

    [Header("代价设置")]
    [Tooltip("区域外（空气）通行代价：鱼不会游出区域")]
    [SerializeField] private float _airCost = 1000f;

    [Tooltip("玩家高代价半径（米）：玩家附近格子代价提升，寻路自动绕开")]
    [SerializeField] private float _playerPenaltyRadius = 3f;

    [Tooltip("玩家高代价上限（格子上与玩家重合时的额外代价）")]
    [SerializeField] private float _playerPenaltyCost = 200f;

    [Tooltip("领地外通行代价：鱼倾向待在共享领地内；须低于玩家代价，玩家紧迫威胁时仍会游出领地逃跑")]
    [SerializeField] private float _territoryOutsideCost = 50f;

    [Header("逃跑设置")]
    [Tooltip("逃跑目标轮换数：从离玩家最远的 N 个安全点中轮换选取，防止路径永远固定被玩家守点")]
    [SerializeField] private int _escapePickCount = 2;

    [Header("群体巡游 (Boids)")]
    [Tooltip("启用 Boids 三力修正漫游方向（聚合/对齐/分离，需挂 FlockMember）。\n关闭时完全退化为纯 A* 漫游（回退开关）")]
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

    [Tooltip("修正强度上限（0~1：三力相对 A* 导航方向的最大混合比例，防三力盖过导航）")]
    [SerializeField, Range(0f, 1f)] private float _flockMaxSteer = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    /// <summary>运行时调试：当前树所处分支（只读）。</summary>
    [Header("运行时调试")]
    [SerializeField, ReadOnly] private string _debugBranch;

    private BubbleFishAI _fish;
    private BTNode _root;
    private string _lastBranch;
    private BTNode.State _lastResult;

    // 群体行为：恐惧传播 + 领头机制（跟随者朝领头逃向）
    private FearSpreader _fearSpreader;

    // 领地：鱼群共享领地（以 FlockId 为 key），安全点在领地分配后生成
    private string _territoryKey;
    private bool _safePointsReady;

    // 群体巡游（Boids）：成员标识 + 邻居查询缓冲（复用，非分配）
    private FlockMember _flockMember;
    private readonly List<FlockMember> _neighbors = new List<FlockMember>(16);

    // 漫游轮换游历状态：到达当前安全点 → 停留 → 切下一个，循环巡游
    private int _wanderIndex;
    private float _wanderArriveUntil;   // 到达当前点后的停留截止时间
    private float _foodArriveUntil;     // 到达食物点后的停留截止时间（避免卡死在食物点）

    // 逃跑目标缓存（每 2s 重选一次，避免每帧换目标）+ 轮换索引（防路径永远固定）
    private Vector2 _escapeTarget;
    private float _escapeTargetTime;
    private int _escapeIndex;
    private const float EscapeTargetRefreshInterval = 2f;

    private void Awake()
    {
        _fish = GetComponent<BubbleFishAI>();
        _fearSpreader = GetComponent<FearSpreader>();
        _flockMember = GetComponent<FlockMember>();

        // Boids 兜底：prefab 未挂 FlockMember 时运行时补挂（Awake 阶段添加，当帧完成群注册）
        if (_flockMember == null && _enableBoids)
            _flockMember = gameObject.AddComponent<FlockMember>();

        // 注册共享领地：同群鱼共用一个领地（以 FlockId 为 key）
        _territoryKey = _flockMember != null ? _flockMember.FlockId : gameObject.tag;
        TerritoryManager.Register(_territoryKey, _fish.SpawnPosition, _regionType, isShared: true);

        Blackboard bb = _fish.Board;
        if (_manualSafePoints != null && _manualSafePoints.Length > 0)
        {
            // 手动安全点：直接采用场景标记点，精确控制鱼的巡游路线（不做随机/区域过滤，需人工保证点在水域内）
            Vector2[] manual = new Vector2[_manualSafePoints.Length];
            for (int i = 0; i < _manualSafePoints.Length; i++)
                manual[i] = _manualSafePoints[i] != null
                    ? (Vector2)_manualSafePoints[i].position
                    : _fish.SpawnPosition;
            bb.SafePoints = manual;
            _safePointsReady = true;
        }
        // 自动安全点延后到领地统一分配完成后再生成（见 Update 首帧），以领地中心替代出生点

        _root = BuildTree();
    }

    /// <summary>
    /// 调试包装：开启调试日志时给节点包一层 BTDebugNode，状态变化时打印节点日志。
    /// 不开启调试时原样返回，零开销。
    /// </summary>
    private BTNode WithDebug(string name, BTNode node)
    {
        return _enableDebugLog ? new BTDebugNode(name, node, this) : node;
    }

    private BTNode BuildTree()
    {
        Blackboard bb = _fish.Board;

        BTNode stunnedBranch = WithDebug("Stunned", new BTSequence(
            WithDebug("Stunned/Cond", new BTCondition(() => bb.IsStunned)),
            WithDebug("Stunned/Action", new BTStunnedAction(_fish))
        ));

        BTNode unstickBranch = WithDebug("Unstick", new BTSequence(
            WithDebug("Unstick/Cond", new BTCondition(() => _fish.IsStuck)),
            WithDebug("Unstick/Action", new BTUnstickAction(_fish))
        ));

        // 逃跑：紧迫威胁 → A* 到离玩家最远的安全点（玩家高代价使路径自动绕开玩家）
        BTNode escapeBranch = WithDebug("Escape", new BTSequence(
            WithDebug("Escape/Cond", new BTCondition(() => bb.IsThreatUrgent)),
            WithDebug("Escape/Action", new BTAStarMoveAction(_fish,
                targetProvider: GetEscapeTarget,
                costAt: CostAt,
                speedMultiplier: _escapeSpeedMultiplier,
                arriveRadius: _arriveRadius,
                repathInterval: _repathInterval,
                move: (direction, mult) => _fish.Swim(direction, mult),
                animResolver: ResolvePathAnimation))
        ));

        // 搜索：威胁已解除但有威胁记忆 → 前往最后已知位置确认（"刚才有动静，去看看"的警惕行为）
        BTNode searchBranch = WithDebug("Search", new BTSequence(
            WithDebug("Search/Cond", new BTCondition(() => bb.ShouldSearch)),
            WithDebug("Search/Action", new BTSearchAction(_fish,
                arriveDistance: 1f,
                speedMultiplier: 1.2f,
                move: (direction, mult) =>
                {
                    _fish.PlayAnimation(AnimalAnimNames.SwimForward);
                    _fish.Swim(direction, mult);
                }))
        ));

        // 漫游：无威胁 → A* 到食物点（若有）否则到择近安全点（带迟滞，多点防抖）
        // 移动方向经 Boids 三力偏移修正（群体协同巡游），动画仍按导航方向解析（反应宏观动向）
        BTNode wanderBranch = WithDebug("Wander", new BTAStarMoveAction(_fish,
            targetProvider: GetWanderTarget,
            costAt: CostAt,
            speedMultiplier: 1f,
            arriveRadius: _arriveRadius,
            repathInterval: _repathInterval,
            move: (direction, mult) => _fish.Swim(ApplyFlockSteering(direction), mult),
            animResolver: ResolvePathAnimation));

        return new BTSelector(stunnedBranch, unstickBranch, escapeBranch, searchBranch, wanderBranch);
    }

    /// <summary>
    /// A* 额外代价：空气（区域外）高代价 + 领地外代价 + 玩家高代价。
    /// 代价层级：空气(1000) > 玩家(200) > 领地外(50) > 正常(0)，
    /// 领地外代价低于玩家代价，保证玩家紧迫威胁时鱼仍会游出领地逃跑。
    /// </summary>
    private float CostAt(Vector2 worldPos)
    {
        float cost = 0f;

        // 空气高代价：不在指定类型区域内
        if (_regionType != AnimalRegion.RegionType.Generic && !IsInsideRegion(worldPos))
            cost += _airCost;

        // 领地外代价：低于玩家代价，鱼倾向待在共享领地内，但不被领地困死
        Territory territory = TerritoryManager.Get(_territoryKey);
        if (territory != null && !territory.Contains(worldPos))
            cost += _territoryOutsideCost;

        // 玩家高代价：玩家可见且在该格附近 → 提升代价，路径绕开
        Blackboard bb = _fish.Board;
        if (bb.IsPlayerVisible)
        {
            Vector2 playerPos = bb.PlayerPosition;
            float d = Vector2.Distance(worldPos, playerPos);
            if (d < _playerPenaltyRadius)
                cost += _playerPenaltyCost * (1f - d / _playerPenaltyRadius);
        }

        return cost;
    }

    /// <summary>世界坐标是否落在指定类型的区域内部（统一走区域注册表查询）。</summary>
    private bool IsInsideRegion(Vector2 worldPos)
    {
        return AnimalRegionRegistry.Contains(worldPos, _regionType);
    }

    /// <summary>
    /// 逃跑目标：跟随者（群内有威胁更高的领头）→ 朝领头游动，群体逃窜方向一致；
    /// 领头 / 无群 → 在"离玩家最远的 N 个安全点"中轮换选取（每 EscapeTargetRefreshInterval 秒重选），
    /// 避免路径永远固定被玩家守点。
    /// </summary>
    private Vector2 GetEscapeTarget()
    {
        // 群体恐慌：跟随领头逃向（领头由 FearSpreader 按群内威胁最高者判定）。
        // 目标实时跟随领头位置，寻路节点按 repathInterval 自动刷新路径。
        if (_fearSpreader != null && _fearSpreader.HasLeader)
            return _fearSpreader.Leader.position;

        if (Time.time < _escapeTargetTime)
            return _escapeTarget;

        _escapeTargetTime = Time.time + EscapeTargetRefreshInterval;

        Blackboard bb = _fish.Board;
        Vector2[] pts = bb.SafePoints;
        if (pts == null || pts.Length == 0)
            return _fish.SpawnPosition;

        Vector2 playerPos = bb.IsPlayerVisible
            ? bb.PlayerPosition
            : _fish.SpawnPosition;

        // 按离玩家距离降序排列索引，取前 _escapePickCount 个轮换选择（防路径固定被守点）
        List<int> ordered = new List<int>(pts.Length);
        for (int i = 0; i < pts.Length; i++)
            ordered.Add(i);
        ordered.Sort((a, b) =>
            Vector2.Distance(pts[b], playerPos).CompareTo(Vector2.Distance(pts[a], playerPos)));

        int pickCount = Mathf.Clamp(_escapePickCount, 1, pts.Length);
        _escapeIndex = (_escapeIndex + 1) % pickCount;
        _escapeTarget = pts[ordered[_escapeIndex]];
        return _escapeTarget;
    }

    /// <summary>
    /// 漫游目标：轮换游历安全点（防抖核心）。
    /// - 有食物 → 去食物点，到达后停留 _wanderDwellTime 再切回安全点巡游（防卡死在食物点）
    /// - 无食物 → 按顺序游历安全点：到达当前点 → 停留 _wanderDwellTime → 切下一个，循环
    /// 不使用 SelectSafePoint 的择近迟滞：那是为逃跑设计的语义，漫游需要"到点换点"。
    /// </summary>
    private Vector2 GetWanderTarget()
    {
        Blackboard bb = _fish.Board;
        Vector2 position = (Vector2)_fish.transform.position;

        // 食物优先：未到达 → 继续去；已到达 → 停留后回退安全点巡游
        if (bb.IsFoodDetected && bb.NearestFood != null)
        {
            Vector2 food = bb.NearestFood.position;
            if (Vector2.Distance(position, food) > _arriveRadius)
                return food; // 还没到，继续游向食物

            // 已到达食物点：首次到达启动停留计时（==0f 表示未计时）
            if (_foodArriveUntil <= 0f)
                _foodArriveUntil = Time.time + _wanderDwellTime;
            if (Time.time < _foodArriveUntil)
                return food; // 停留中：鱼在食物点停留觅食

            // 停留结束 → 清计时，切回安全点轮换（不重置索引，从下一个点继续）
            _foodArriveUntil = 0f;
            _wanderIndex = (_wanderIndex + 1) % Mathf.Max(1, bb.SafePoints?.Length ?? 1);
        }

        Vector2[] pts = bb.SafePoints;
        if (pts == null || pts.Length == 0)
            return _fish.SpawnPosition;

        Vector2 current = pts[_wanderIndex % pts.Length];

        // 已到达当前安全点 → 停留 _wanderDwellTime 再切下一个（循环巡游）
        if (Vector2.Distance(position, current) <= _arriveRadius)
        {
            // 首次到达启动停留计时（==0f 表示未计时，不能靠 Time.time 比较，否则停留结束后会被无限重置）
            if (_wanderArriveUntil <= 0f)
                _wanderArriveUntil = Time.time + _wanderDwellTime;
            if (Time.time < _wanderArriveUntil)
                return current; // 停留中：目标保持当前点，鱼停在此处

            // 停留结束 → 清计时，切下一个点
            _wanderArriveUntil = 0f;
            _wanderIndex = (_wanderIndex + 1) % pts.Length;
            current = pts[_wanderIndex % pts.Length];
        }
        else
        {
            // 还没到达：重置停留计时（避免游动途中残留旧计时导致到点立即切走）
            _wanderArriveUntil = 0f;
        }

        return current;
    }

    /// <summary>
    /// Boids 三力修正：仅作用于漫游分支，聚合/对齐/分离叠加在 A* 导航方向上（只做偏移修正，
    /// 目标导航仍由 A* 负责）。未挂 FlockMember、功能关闭或无邻居时原样返回，完全退化为纯 A* 漫游。
    /// </summary>
    private Vector2 ApplyFlockSteering(Vector2 direction)
    {
        if (!_enableBoids || _flockMember == null)
            return direction;

        if (FlockManager.GetNeighbors(_flockMember, _flockNeighborRadius, _neighbors) == 0)
            return direction;

        return BoidsSteering.Apply(direction, _flockMember.transform.position, _flockMember.Velocity,
            _neighbors, _flockSeparationRadius, _separationWeight, _alignmentWeight,
            _cohesionWeight, _flockMaxSteer);
    }

    /// <summary>
    /// 依据移动方向解析动画状态名：垂直为主→上浮/下沉，否则→前行。
    /// </summary>
    private string ResolvePathAnimation(Vector2 segmentDirection)
    {
        if (Mathf.Abs(segmentDirection.y) > Mathf.Abs(segmentDirection.x))
            return segmentDirection.y > 0f ? AnimalAnimNames.SwimUp : AnimalAnimNames.SwimDown;
        return AnimalAnimNames.SwimForward;
    }

    private void Update()
    {
        if (_root == null)
        {
            _fish = GetComponent<BubbleFishAI>();
            _root = BuildTree();
        }

        // 首帧：所有动物 Awake 完成后统一分配领地，再用领地中心生成安全点（替代出生点）
        if (!_safePointsReady)
        {
            TerritoryManager.EnsureAssigned();
            GenerateSafePointsFromTerritory();
        }

        // 低频威胁源动态化：玩家明显移动后微调领地中心（内部节流，几乎零开销）
        TerritoryManager.RefreshForThreat();

        BTNode.State result = _root.Tick();

        _debugBranch = GetBranchName();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    /// <summary>
    /// 用共享领地中心生成安全点（领地分配完成后调用）；领地未分配时回退到出生点。
    /// </summary>
    private void GenerateSafePointsFromTerritory()
    {
        Blackboard bb = _fish.Board;
        Territory territory = TerritoryManager.Get(_territoryKey);
        Vector2 center = territory != null ? territory.Center : _fish.SpawnPosition;
        float radius = territory != null ? territory.Radius : _safePointRadius;

        bb.SafePoints = SafePointGenerator.GenerateSafePoints(
            center, _safePointCount, radius, _regionType, _safePointSpacing);
        _safePointsReady = true;
    }

    /// <summary>
    /// 计算当前树所处分支名，供运行时调试显示。
    /// </summary>
    private string GetBranchName()
    {
        Blackboard bb = _fish.Board;

        if (bb.IsStunned) return "Stunned";
        if (_fish.IsStuck) return "Unstick";
        if (bb.IsThreatUrgent) return "Escape";
        if (bb.ShouldSearch) return "Search";
        return "Wander";
    }

    private void LogStateChange(BTNode.State result)
    {
        Blackboard bb = _fish.Board;

        string branch = GetBranchName();

        if (branch == _lastBranch && result == _lastResult)
            return;

        // 含威胁记忆溯源：玩家不可见却处于 Escape/Search 时，多半是恐惧记忆（FearSpreader 注入或感知残留）所致
        Debug.Log($"{gameObject.name} BT: [{branch}] 距玩家[{bb.PlayerDistance:F1}m] 威胁[{bb.ThreatLevel:F0}] " +
                  $"玩家可见[{bb.IsPlayerVisible}] 记忆位置[{bb.LastKnownPlayerPos}] " +
                  $"领头[{(_fearSpreader != null && _fearSpreader.HasLeader ? _fearSpreader.Leader.name : "无")}]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
