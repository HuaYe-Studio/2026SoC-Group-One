using UnityEngine;

/// <summary>
/// [BT] 青蛙行为树：挂载并启用后自动接管（禁用）FSM，用行为树驱动青蛙AI。
/// 优先级从高到低：逃跑 > 捕食 > 觅食循环（跳一次 → 休息）。
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
        BTNode fleeBranch = new BTSequence(
            new BTCondition(() => _frog.IsPlayerDetected),
            new BTFleeAction(_frog));

        // 分支2：检测到食物 → 捕食
        BTNode pounceBranch = new BTSequence(
            new BTCondition(() => _frog.IsFoodDetected),
            new BTPounceAction(_frog));

        // 分支3：默认觅食循环 → 跳一次 → 休息几秒 → 循环
        BTNode forageBranch = new BTSequence(
            new BTHopAction(_frog),
            new BTRestAction(_frog, _restDurationMin, _restDurationMax));

        return new BTSelector(fleeBranch, pounceBranch, forageBranch);
    }

    private void Update()
    {
        BTNode.State result = _root.Tick();

        if (_enableDebugLog)
            Debug.Log($"{gameObject.name} BT Tick: {result}");
    }
}
