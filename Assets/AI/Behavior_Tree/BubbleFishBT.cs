using UnityEngine;

/// <summary>
/// [BT] 泡泡鱼行为树。挂载后自动接管（禁用）FSM。
/// 优先级：被吞噬眩晕 > 太近回避 > 绕玩家巡游 > 自由巡游。
/// 设计思路：鱼不"看到玩家就跑远"，而是绕玩家自然游动，太近时轻轻避开一小段，
/// 给玩家留出接近窗口，也不会被玩家压在一侧。
/// </summary>
[RequireComponent(typeof(BubbleFishAI))]
public class BubbleFishBT : MonoBehaviour
{
    [Header("回避设置")]
    [Tooltip("距玩家多近开始回避（米）")]
    [SerializeField] private float _avoidDistance = 2f;
    [Tooltip("每次回避游多远（米）")]
    [SerializeField] private float _avoidStep = 1.5f;

    [Header("巡游设置")]
    [Tooltip("自由巡游范围半径（米），超出后偏向出生点")]
    [SerializeField] private float _wanderRange = 6f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private BubbleFishAI _fish;
    private BTNode _root;
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void Awake()
    {
        _fish = GetComponent<BubbleFishAI>();

        FSM fsm = GetComponent<FSM>();
        if (fsm != null)
            fsm.enabled = false;

        _root = BuildTree();
    }

    private BTNode BuildTree()
    {
        BTNode stunnedBranch = new BTSequence(
            new BTCondition(() => _fish.IsDevoured),
            new BTStunnedAction(_fish, 0.5f),
            new BTAction(() =>
            {
                _fish.ClearDevoured();
                return BTNode.State.Success;
            })
        );

        BTNode avoidBranch = new BTSequence(
            new BTCondition(() => _fish.IsPlayerDetected && _fish.PlayerDistance < _avoidDistance),
            new BTAvoidAction(_fish, _avoidStep)
        );

        BTNode circleBranch = new BTSequence(
            new BTCondition(() => _fish.IsPlayerDetected),
            new BTCircleAroundAction(_fish)
        );

        BTNode wanderBranch = new BTWanderAction(_fish, _wanderRange);

        return new BTSelector(stunnedBranch, avoidBranch, circleBranch, wanderBranch);
    }

    private void Update()
    {
        if (_root == null)
        {
            _fish = GetComponent<BubbleFishAI>();
            _root = BuildTree();
        }

        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    private void LogStateChange(BTNode.State result)
    {
        string branch = _fish.IsDevoured ? "Stunned"
            : _fish.IsPlayerDetected && _fish.PlayerDistance < _avoidDistance ? "Avoid"
            : _fish.IsPlayerDetected ? "Circle"
            : "Wander";

        if (branch == _lastBranch && result == _lastResult)
            return;

        Debug.Log($"{gameObject.name} BT: [{branch}] 距玩家[{_fish.PlayerDistance:F1}m]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
