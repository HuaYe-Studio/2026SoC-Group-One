using UnityEngine;

/// <summary>
/// [BT] 泡泡鱼行为树：挂载并启用后自动接管（禁用）FSM，用行为树驱动泡泡鱼AI。
/// 优先级从高到低：被吞噬眩晕 > 逃跑 > 默认游动。
/// 动画统一由 BubbleFishAI.PlayAnimation 控制 Animator 整数参数
/// BubbleState：0=Idle 1=Flee 2=Expanded 3=Stunned。
/// </summary>
[RequireComponent(typeof(BubbleFishAI))]
public class BubbleFishBT : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private BubbleFishAI _fish;
    private BTNode _root;

    // 调试用：记录上次日志的分支/结果，只在变化时输出
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void Awake()
    {
        _fish = GetComponent<BubbleFishAI>();

        // 行为树与状态机同一时间只能有一套驱动AI，启用本组件时关闭 FSM
        FSM fsm = GetComponent<FSM>();
        if (fsm != null)
            fsm.enabled = false;

        _root = BuildTree();
    }

    /// <summary>
    /// 组装泡泡鱼行为树。感知数据复用 AnimalBase / BubbleFishAI。
    /// </summary>
    private BTNode BuildTree()
    {
        // 分支1：被吞噬 → 眩晕 0.5s（最高优先级）
        BTNode stunnedBranch = new BTSequence(
            new BTCondition(() => _fish.IsDevoured),
            new BTStunnedAction(_fish, 0.5f),
            new BTAction(() =>
            {
                _fish.ClearDevoured();
                return BTNode.State.Success;
            })
        );

        // 分支2：检测到玩家 → 逃跑
        BTNode fleeBranch = new BTSequence(
            new BTCondition(() => _fish.IsPlayerDetected),
            new BTFleeAction(_fish));

        // 分支3：默认游动
        BTNode swimBranch = new BTSwimAction(_fish);

        return new BTSelector(stunnedBranch, fleeBranch, swimBranch);
    }

    private void Update()
    {
        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _fish = GetComponent<BubbleFishAI>();
            _root = BuildTree();
        }

        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    /// <summary>
    /// 只在分支或结果发生变化时输出日志，避免刷屏。
    /// </summary>
    private void LogStateChange(BTNode.State result)
    {
        string branch = _fish.IsDevoured ? "Stunned眩晕"
            : _fish.IsPlayerDetected ? "Flee逃跑"
            : "Swim游动";

        if (branch == _lastBranch && result == _lastResult)
            return;

        Debug.Log($"{gameObject.name} BT: 分支[{branch}] 结果[{result}]");
        _lastBranch = branch;
        _lastResult = result;
    }
}
