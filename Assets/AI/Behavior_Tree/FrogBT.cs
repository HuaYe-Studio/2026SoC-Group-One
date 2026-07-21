using UnityEngine;

/// <summary>
/// [BT] 青蛙行为树：挂载并启用后自动接管（禁用）FSM，用行为树驱动青蛙AI。
/// 优先级从高到低：逃跑 > 捕食 > 觅食循环（落地休息 → 跳一次）。
/// 动画统一由 FrogAI.PlayAnimation 控制 Animator 整数参数
/// FROG_AnimState：0=Idle 1=Jump 2=Rest 3=Flee 4=Prey。
/// </summary>
[RequireComponent(typeof(FrogAI))]
public class FrogBT : MonoBehaviour
{
    [Header("Rest")]
    [SerializeField] private float _restDurationMin = 3f;
    [SerializeField] private float _restDurationMax = 6f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private FrogAI _frog;
    private BTNode _root;
    private BTFleeAction _fleeAction; // 持有引用以便调试时读取内部状态

    // 调试用：记录上次日志的分支/结果/着地，只在变化时输出
    private string _lastBranch;
    private BTNode.State _lastResult;
    private bool _lastGrounded;

    private void Awake()
    {
        _frog = GetComponent<FrogAI>();

        // 行为树与状态机同一时间只能有一套驱动AI，启用本组件时关闭 FSM
        FSM fsm = GetComponent<FSM>();
        if (fsm != null)
            fsm.enabled = false;

        _root = BuildTree();
    }

    /// <summary>
    /// 组装青蛙行为树。感知数据复用 AnimalBase / EnvironmentMonitor。
    /// </summary>
    private BTNode BuildTree()
    {
        // 分支1：检测到玩家 → 逃跑（最高优先级）
        _fleeAction = new BTFleeAction(_frog);
        BTNode fleeBranch = new BTSequence(
            new BTCondition(() => _frog.IsPlayerDetected),
            _fleeAction);

        // 分支2：检测到食物 → 捕食
        BTNode pounceBranch = new BTSequence(
            new BTCondition(() => _frog.IsFoodDetected),
            new BTPounceAction(_frog));

        // 分支3：默认觅食循环 → 落地后先休息几秒 → 跳一次 → 落地再休息，循环
        BTNode forageBranch = new BTSequence(
            new BTRestAction(_frog, _restDurationMin, _restDurationMax),
            new BTHopAction(_frog, () => _enableDebugLog));

        return new BTSelector(fleeBranch, pounceBranch, forageBranch);
    }

    private void Update()
    {
        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _frog = GetComponent<FrogAI>();
            _root = BuildTree();
        }

        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            LogStateChange(result);
    }

    /// <summary>
    /// 只在分支、结果或着地状态发生变化时输出日志，避免刷屏。
    /// 逃跑时额外输出紧迫度/玩家速度/地形信息。
    /// </summary>
    private void LogStateChange(BTNode.State result)
    {
        string branch = _frog.IsPlayerDetected ? "Flee逃跑"
            : _frog.IsFoodDetected ? "Pounce捕食"
            : "Forage觅食";

        bool grounded = _frog.IsGrounded;

        if (branch == _lastBranch && result == _lastResult && grounded == _lastGrounded)
            return;

        string extra = "";
        if (branch == "Flee逃跑" && _fleeAction != null)
            extra = $" 紧迫度[{_fleeAction.UrgencyLevel}] 玩家速度[{_fleeAction.PlayerVelocity.x:F1}]" +
                    $" 墙[{( _frog.Monitor != null && _frog.Monitor.IsWallAhead ? "有" : "无" )}]" +
                    $" 沟[{( _frog.Monitor != null && _frog.Monitor.IsGapAhead ? "有" : "无" )}]";

        Debug.Log($"{gameObject.name} BT: 分支[{branch}] 结果[{result}] 着地[{grounded}]{extra}");
        _lastBranch = branch;
        _lastResult = result;
        _lastGrounded = grounded;
    }
}
