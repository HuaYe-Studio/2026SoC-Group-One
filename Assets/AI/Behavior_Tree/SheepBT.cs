using UnityEngine;

/// <summary>
/// [BT] 冲冲羊行为树：挂载并启用后驱动冲冲羊AI。
/// 性格：中立动物——面对玩家不逃跑也不主动攻击。
/// 攻击触发（通用复仇，由 RevengeBehavior 组件驱动，目标 = 攻击者）：
///   - 自己被任何来源攻击（玩家吞噬、敌方误伤等）
///   - 感知范围内同类（同 Tag）被攻击
/// 复仇持续一段时间后恢复中立。
/// 优先级从高到低：眩晕 > 脱困 > 复仇冲撞 > 巡游。
/// 感知数据统一从 Blackboard 读取语义化认知状态。
/// </summary>
[RequireComponent(typeof(SheepAI))]
[RequireComponent(typeof(RevengeBehavior))]
public class SheepBT : MonoBehaviour
{
    [Header("Wander")]
    [Tooltip("自由巡游范围半径（米）")]
    [SerializeField] private float _wanderRange = 5f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private SheepAI _sheep;
    private RevengeBehavior _revenge;
    private BTNode _root;

    // 调试用：只在分支/结果变化时输出日志
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void Awake()
    {
        _sheep = GetComponent<SheepAI>();
        _revenge = GetComponent<RevengeBehavior>();
        _root = BuildTree();
    }

    private BTNode BuildTree()
    {
        Blackboard bb = _sheep.Board;

        // 分支0：被吞噬/受击眩晕中 → 原地僵直（最高优先级）
        BTNode stunnedBranch = new BTSequence(
            new BTCondition(() => bb.IsStunned),
            new BTStunnedAction(_sheep));

        // 分支1：卡死 → 脱困（仅次于眩晕）
        BTNode unstickBranch = new BTSequence(
            new BTCondition(() => _sheep.IsStuck),
            new BTUnstickAction(_sheep));

        // 分支2：复仇状态且复仇目标存活 → 追猎 + 冲撞攻击（目标抽象：攻击者，不限玩家）
        BTNode revengeBranch = new BTSequence(
            new BTCondition(() => _revenge.IsRevenge && _revenge.RevengeTarget != null),
            new BTChargeAction(_sheep,
                hasTarget: () => _revenge.IsRevenge && _revenge.RevengeTarget != null,
                targetPos: () => (Vector2)_revenge.RevengeTarget.transform.position));

        // 分支3：默认自由巡游（中立：不逃跑、不搜索）
        BTNode wanderBranch = new BTWanderAction(_sheep, _wanderRange);

        return new BTSelector(stunnedBranch, unstickBranch, revengeBranch, wanderBranch);
    }

    private void Update()
    {
        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _sheep = GetComponent<SheepAI>();
            _revenge = GetComponent<RevengeBehavior>();
            _root = BuildTree();
        }

        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    private void LogStateChange(BTNode.State result)
    {
        Blackboard bb = _sheep.Board;

        string branch = bb.IsStunned ? "Stunned眩晕"
            : _sheep.IsStuck ? "Unstick脱困"
            : _revenge.IsRevenge && _revenge.RevengeTarget != null ? "Revenge复仇"
            : "Wander巡游";

        if (branch == _lastBranch && result == _lastResult)
            return;

        Debug.Log($"{gameObject.name} BT: 分支[{branch}] 结果[{result}] " +
                  $"复仇[{(_revenge.IsRevenge ? "是" : "否")}] 目标[{(_revenge.RevengeTarget != null ? _revenge.RevengeTarget.name : "无")}]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
