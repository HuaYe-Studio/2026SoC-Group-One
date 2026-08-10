using UnityEngine;

/// <summary>
/// [BT] 泡泡鱼行为树：挂载并启用后驱动泡泡鱼AI。
/// 优先级：被吞噬眩晕 > 脱困 > 路径逃生(按威胁方向选路) > 直接逃跑(无路径兜底) >
///         搜索(威胁记忆) > 回撤(回安全位置) > 路径巡游 > 自由巡游。
/// 设计思路：面对威胁时鱼沿预先绘制好的逃生路径撤离，撤离路径按威胁方向自动选择；
/// 威胁解除后若仍有威胁记忆则先前往最后已知位置搜索确认，
/// 再返回安全位置（巡游起点或出生点），最后继续巡游。
/// 所有感知判断统一从 Blackboard 读取语义化认知状态。
/// </summary>
[RequireComponent(typeof(BubbleFishAI))]
public class BubbleFishBT : MonoBehaviour
{
    [Header("逃生设置")]
    [Tooltip("逃生路径列表（FishPath.Type = Escape）。留空时自动收集场景中所有 Type=Escape 的路径（预制体友好，无需手动拖引用）")]
    [SerializeField] private FishPath[] _escapePaths;

    [Tooltip("障碍物层：被这些层阻挡的逃生路径会在选路时被剔除（威胁方向 + 障碍物双重判断）。None=不检测")]
    [SerializeField] private LayerMask _obstacleMask;

    [Tooltip("重新选路的间隔（秒），期间保持当前路径")]
    [SerializeField] private float _redecideInterval = 1.5f;

    [Tooltip("重新决策时给非当前路径的偏好加分，越大越倾向换新路径")]
    [SerializeField] private float _newPathBias = 0.15f;

    [Header("回撤设置")]
    [Tooltip("脱离危险后返回逃生起点的速度倍率")]
    [SerializeField] private float _retreatSpeedMultiplier = 1f;

    [Tooltip("到达逃生起点（或途经点）的判定半径（米）")]
    [SerializeField] private float _retreatArriveRadius = 0.4f;

    [Header("巡游设置")]
    [Tooltip("自由巡游范围半径（米），超出后偏向出生点")]
    [SerializeField] private float _wanderRange = 6f;

    [Header("路径巡游")]
    [Tooltip("绘制好的巡游路径（FishPath.Type = Normal）。为空时回退为自由巡游")]
    [SerializeField] private FishPath _path;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    /// <summary>运行时调试：当前被选中的逃生路径（只读，运行中查看）。</summary>
    [Header("运行时调试")]
    [SerializeField, ReadOnly] private FishPath _debugCurrentEscapePath;

    /// <summary>运行时调试：当前树所处分支（只读）。</summary>
    [SerializeField, ReadOnly] private string _debugBranch;

    private BubbleFishAI _fish;
    private BTNode _root;
    private string _lastBranch;
    private BTNode.State _lastResult;

    // 逃生节点引用，用于运行时调试读取当前选中路径
    private BTPathEscapeAction _escapeAction;

    private void Awake()
    {
        _fish = GetComponent<BubbleFishAI>();

        ResolveEscapePaths();
        _root = BuildTree();
    }

    /// <summary>
    /// 逃生路径解析：数组留空时自动收集场景中所有 Type=Escape 的 FishPath。
    /// 这样逃生路径做成独立预制体放进场景即可，每条鱼无需手动拖引用。
    /// </summary>
    private void ResolveEscapePaths()
    {
        if (_escapePaths != null && _escapePaths.Length > 0)
            return;

        var found = UnityEngine.Object.FindObjectsByType<FishPath>(FindObjectsSortMode.None);
        System.Collections.Generic.List<FishPath> list = new System.Collections.Generic.List<FishPath>();
        foreach (FishPath path in found)
        {
            if (path != null && path.Type == FishPath.PathType.Escape && path.Points != null && path.Points.Count >= 2)
                list.Add(path);
        }
        _escapePaths = list.ToArray();
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

        // 逃生：紧迫威胁且有逃生路径 → 按威胁方向选路并沿路径撤离
        // 回撤目标：威胁解除后回到巡游路径第 0 点（从起点重新巡游）；无巡游路径时回退为逃生起点
        _escapeAction = new BTPathEscapeAction(_fish, _escapePaths,
            speedMultiplier: _fish.FleeSpeedMultiplier,
            redecideInterval: _redecideInterval,
            newPathBias: _newPathBias,
            move: (direction, mult) => _fish.Swim(direction, mult),
            animResolver: ResolvePathAnimation,
            obstacleMask: _obstacleMask,
            retreatTargetProvider: () =>
            {
                // 有巡游路径 → 回到巡游路径第 0 点（从起点重新巡游）
                if (_path != null && _path.Points != null && _path.Points.Count > 0)
                    return _path.Points[0];
                // 无巡游路径 → 回撤到出生点（而非威胁发生时的位置），
                // 避免鱼在玩家离开后游回"玩家身边"来回折腾
                return _fish.SpawnPosition;
            });

        // 逃生：紧迫威胁 → 有逃生路径时按威胁方向选路并沿路径撤离；
        // 无逃生路径时回退为 BTFleeAction 直接朝远离玩家方向逃跑（避免"没配路径就不逃"的缺口）
        BTNode directFlee = WithDebug("Escape/Direct", new BTSequence(
            WithDebug("Escape/Direct/Cond", new BTCondition(() => bb.IsThreatUrgent)),
            WithDebug("Escape/Direct/Action", new BTFleeAction(_fish))
        ));

        BTNode escapeBranch = WithDebug("EscapePath", new BTSelector(
            WithDebug("Escape/Path", new BTSequence(
                WithDebug("Escape/Cond", new BTCondition(() => bb.IsThreatUrgent && HasEscapePaths)),
                WithDebug("Escape/Action", _escapeAction))),
            directFlee
        ));

        // 搜索：威胁已解除但有威胁记忆 → 前往最后已知位置确认（"刚才有动静，去看看"的警惕行为）
        BTNode searchBranch = WithDebug("Search", new BTSequence(
            WithDebug("Search/Cond", new BTCondition(() => bb.ShouldSearch)),
            WithDebug("Search/Action", new BTSearchAction(_fish,
                arriveDistance: 1f,
                speedMultiplier: 1.2f,
                move: (direction, mult) =>
                {
                    _fish.PlayAnimation("SwimForward");
                    _fish.Swim(direction, mult);
                }))
        ));

        // 回撤：威胁已解除且存在未完成的回撤目标 → 回安全位置（巡游起点或出生点，直线/折线）
        // 玩家避让：玩家守在基准回撤点太近时，沿路径顺延到远离玩家的点（无路径则镜像到玩家另一侧的出生点）
        BTNode retreatBranch = WithDebug("Retreat", new BTSequence(
            WithDebug("Retreat/Cond", new BTCondition(() => !bb.IsThreatUrgent && bb.HasRetreatTarget)),
            WithDebug("Retreat/Action", new BTReturnToPointAction(_fish,
                speedMultiplier: _retreatSpeedMultiplier,
                arriveRadius: _retreatArriveRadius,
                move: (direction, mult) => _fish.Swim(direction, mult),
                animResolver: ResolvePathAnimation,
                obstacleLayers: _obstacleMask,
                destinationResolver: ResolveRetreatDestination))
        ));

        BTNode wanderBranch = WithDebug("Wander", new BTWanderAction(_fish, _wanderRange, _obstacleMask));

        // 路径巡游：绘制好的路径优先，路径为空时回退为自由巡游
        // 通过委托注入移动方式(Swim)与动画解析(段走向→动画名)，BTPathFollowAction 保持通用
        BTNode pathBranch = WithDebug("Path", _path != null
            ? new BTPathFollowAction(_fish, _path,
                move: (direction, mult) => _fish.Swim(direction, mult),
                animResolver: ResolvePathAnimation)
            : wanderBranch);

        return new BTSelector(stunnedBranch, unstickBranch, escapeBranch, searchBranch, retreatBranch, pathBranch);
    }

    /// <summary>
    /// 是否存在至少一条有效逃生路径。
    /// </summary>
    private bool HasEscapePaths
    {
        get
        {
            if (_escapePaths == null || _escapePaths.Length == 0)
                return false;
            foreach (FishPath path in _escapePaths)
            {
                if (path != null && path.Points != null && path.Points.Count >= 2)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 依据路径段走向解析动画状态名：垂直为主→上浮/下沉，否则→前行。
    /// </summary>
    private string ResolvePathAnimation(Vector2 segmentDirection)
    {
        if (Mathf.Abs(segmentDirection.y) > Mathf.Abs(segmentDirection.x))
            return segmentDirection.y > 0f ? "SwimUp" : "SwimDown";
        return "SwimForward";
    }

    /// <summary>
    /// 回撤目的地解析：玩家守在基准回撤点太近时，顺延到远离玩家的点，避免回撤撞玩家。
    /// 有巡游路径：沿路径顺延（从当前位置吸附最近点，向远离玩家方向推进 SafeRadius 弧长）；
    /// 无路径：取出生点在玩家另一侧的镜像点。玩家不可见时直接用基准目标。
    /// </summary>
    /// <param name="baseTarget">基准回撤目标（巡游起点或出生点）</param>
    private Vector2 ResolveRetreatDestination(Vector2 baseTarget)
    {
        Blackboard bb = _fish.Board;
        Vector2 playerPos = bb.IsPlayerVisible
            ? bb.AnimalPosition + bb.PlayerDirection * bb.PlayerDistance
            : bb.LastKnownPlayerPos;

        // 玩家位置无效或离基准回撤点足够远 → 直接回基准点
        if (playerPos == Vector2.zero || Vector2.Distance(playerPos, baseTarget) >= bb.SafeRadius)
            return baseTarget;

        // 有巡游路径：从鱼当前位置吸附路径最近点，向远离玩家方向顺延 SafeRadius 弧长
        if (_path != null && _path.Points != null && _path.Points.Count >= 2 && _path.TotalLength > 0.01f)
        {
            float startT = _path.NearestT(_fish.transform.position);
            float deltaT = bb.SafeRadius / _path.TotalLength;
            float bestT = startT;
            float bestDist = Vector2.Distance(playerPos, _path.SamplePoint(startT));
            foreach (float sign in new[] { 1f, -1f })
            {
                float t = startT + sign * deltaT;
                if (_path.Loop) t = Mathf.Repeat(t, 1f); else t = Mathf.Clamp01(t);
                float d = Vector2.Distance(playerPos, _path.SamplePoint(t));
                if (d > bestDist) { bestDist = d; bestT = t; }
            }
            return _path.SamplePoint(bestT);
        }

        // 无路径：出生点在玩家另一侧的镜像点（玩家守在出生点旁时，回撤到玩家对面）
        Vector2 spawn = _fish.SpawnPosition;
        Vector2 toSpawn = spawn - playerPos;
        float mirrorSide = toSpawn.x >= 0f ? 1f : -1f;
        return new Vector2(playerPos.x + mirrorSide * bb.SafeRadius, spawn.y);
    }

    private void Update()
    {
        if (_root == null)
        {
            _fish = GetComponent<BubbleFishAI>();
            _root = BuildTree();
        }

        BTNode.State result = _root.Tick();

        // 刷新运行时调试字段（Inspector 中只读查看）
        _debugCurrentEscapePath = _escapeAction != null ? _escapeAction.CurrentPath : null;
        _debugBranch = GetBranchName();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    /// <summary>
    /// 计算当前树所处分支名，供运行时调试显示。
    /// </summary>
    private string GetBranchName()
    {
        Blackboard bb = _fish.Board;

        if (bb.IsStunned) return "Stunned";
        if (_fish.IsStuck) return "Unstick";
        if (bb.IsThreatUrgent && HasEscapePaths) return "EscapePath";
        if (bb.IsThreatUrgent) return "EscapeDirect";
        if (bb.ShouldSearch) return "Search";
        if (!bb.IsThreatUrgent && bb.HasRetreatTarget) return "Retreat";
        if (_path != null) return "Path";
        return "Wander";
    }

    private void LogStateChange(BTNode.State result)
    {
        Blackboard bb = _fish.Board;

        string branch = GetBranchName();

        if (branch == _lastBranch && result == _lastResult)
            return;

        Debug.Log($"{gameObject.name} BT: [{branch}] 距玩家[{bb.PlayerDistance:F1}m] 威胁值[{bb.ThreatLevel:F0}]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
