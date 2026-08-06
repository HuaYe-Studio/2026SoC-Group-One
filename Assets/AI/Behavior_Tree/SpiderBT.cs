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
    [Header("Chase")]
    [Tooltip("追捕玩家速度倍率")]
    [SerializeField] private float _chaseSpeedMultiplier = 1.2f;

    [Header("Search")]
    [Tooltip("威胁值高于此值时触发搜索行为")]
    [SerializeField] private float _searchThreatThreshold = 30f;

    [Header("Wander")]
    [Tooltip("自由巡游范围半径（米）")]
    [SerializeField] private float _wanderRange = 5f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private SpiderAI _spider;
    private BTNode _root;

    // 调试用：只在分支/结果变化时输出日志
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void Awake()
    {
        _spider = GetComponent<SpiderAI>();
        _root = BuildTree();
    }

    private BTNode BuildTree()
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

        // 分支2：玩家可见且非同形态 → 追捕（敌对：不依赖威胁值；玩家同为蜘蛛形态时友好，不追捕）
        BTNode chaseBranch = new BTSequence(
            new BTCondition(() => bb.IsPlayerVisible && !bb.IsPlayerSameForm),
            new BTChasePlayerAction(_spider, _chaseSpeedMultiplier));

        // 分支3：玩家不可见但有威胁记忆 → 前往最后已知位置搜索
        BTNode searchBranch = new BTSequence(
            new BTCondition(() => bb.ShouldSearch && bb.ThreatLevel >= _searchThreatThreshold),
            new BTSearchAction(_spider, 1f, 1.2f));

        // 分支4：默认自由巡游
        BTNode wanderBranch = new BTWanderAction(_spider, _wanderRange);

        return new BTSelector(stunnedBranch, unstickBranch, chaseBranch, searchBranch, wanderBranch);
    }

    private void Update()
    {
        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _spider = GetComponent<SpiderAI>();
            _root = BuildTree();
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
