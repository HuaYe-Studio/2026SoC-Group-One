using UnityEngine;

/// <summary>
/// [BT] 冲冲羊行为树：挂载并启用后驱动冲冲羊AI。
/// 性格：中立动物——面对玩家不逃跑也不主动攻击。
/// 唯一攻击触发：玩家吞噬了附近同类（同是 SheepAI）时，羊进入复仇状态，
/// 追猎玩家并用冲撞攻击；复仇持续一段时间后恢复中立。
/// 优先级从高到低：眩晕 > 脱困 > 复仇冲撞（同类被吃且玩家可见）> 巡游。
/// 感知数据统一从 Blackboard 读取语义化认知状态。
/// </summary>
[RequireComponent(typeof(SheepAI))]
public class SheepBT : MonoBehaviour
{
    [Header("Revenge")]
    [Tooltip("同类被吞噬的感知范围（米）：范围内同类被吃才会触发复仇")]
    [SerializeField] private float _revengeSenseRadius = 10f;

    [Tooltip("复仇状态持续时长（秒）：超时后恢复中立")]
    [SerializeField] private float _revengeDuration = 6f;

    [Header("Wander")]
    [Tooltip("自由巡游范围半径（米）")]
    [SerializeField] private float _wanderRange = 5f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private SheepAI _sheep;
    private BTNode _root;

    // 复仇状态
    private bool _isRevenge;
    private float _revengeUntil;

    // 调试用：只在分支/结果变化时输出日志
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void Awake()
    {
        _sheep = GetComponent<SheepAI>();
        _root = BuildTree();
    }

    private void OnEnable()
    {
        MockEventCenter.OnAnimalDevoured += HandleAnimalDevoured;
    }

    private void OnDisable()
    {
        MockEventCenter.OnAnimalDevoured -= HandleAnimalDevoured;
    }

    /// <summary>
    /// 同类被吞噬回调：感知范围内有 SheepAI 被玩家吃掉 → 进入复仇状态。
    /// </summary>
    private void HandleAnimalDevoured(GameObject victim)
    {
        if (victim == null || victim == gameObject)
            return;

        // 只对同类（同为冲冲羊）的被吞噬做出反应
        if (victim.GetComponent<SheepAI>() == null)
            return;

        // 距离在感知范围内才触发复仇
        if (Vector2.Distance(transform.position, victim.transform.position) > _revengeSenseRadius)
            return;

        _isRevenge = true;
        _revengeUntil = Time.time + _revengeDuration;
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

        // 分支2：复仇状态且玩家可见 → 追猎 + 冲撞攻击
        BTNode revengeBranch = new BTSequence(
            new BTCondition(() => _isRevenge && bb.IsPlayerVisible),
            new BTChargeAction(_sheep));

        // 分支3：默认自由巡游（中立：不逃跑、不搜索）
        BTNode wanderBranch = new BTWanderAction(_sheep, _wanderRange);

        return new BTSelector(stunnedBranch, unstickBranch, revengeBranch, wanderBranch);
    }

    private void Update()
    {
        // 复仇超时 → 恢复中立
        if (_isRevenge && Time.time >= _revengeUntil)
            _isRevenge = false;

        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _sheep = GetComponent<SheepAI>();
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
            : _isRevenge && bb.IsPlayerVisible ? "Revenge复仇"
            : "Wander巡游";

        if (branch == _lastBranch && result == _lastResult)
            return;

        Debug.Log($"{gameObject.name} BT: 分支[{branch}] 结果[{result}] " +
                  $"复仇[{(_isRevenge ? "是" : "否")}] 距玩家[{bb.PlayerDistance:F1}m]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
