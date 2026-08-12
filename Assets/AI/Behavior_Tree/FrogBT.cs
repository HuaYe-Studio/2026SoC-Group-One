using UnityEngine;

/// <summary>
/// [BT] 青蛙行为树：挂载并启用后驱动青蛙AI。
/// 优先级从高到低：逃跑 > 搜索（威胁记忆）> 捕食 > 觅食循环（连跳一组 → 喘息）。
/// 连跳组：组内每次跳跃高度递减、间隔缩短，模仿真实青蛙急促连跳；
/// 喘息：一组跳完的短暂停顿（Rest 动画），与吞噬眩晕(IsStunned)互斥。
/// 捕食/逃跑保持独立节奏：捕食单次扑跳，逃跑按紧迫度升级，不参与连跳组。
/// 所有感知判断统一从 Blackboard 读取语义化认知状态。
/// 动画统一由 FrogAI.PlayAnimation 控制 Animator 整数参数
/// FROG_AnimState：0=Idle 1=Jump 2=Rest 3=Flee 4=Prey。
/// </summary>
[RequireComponent(typeof(FrogAI))]
public class FrogBT : MonoBehaviour
{
    [Header("Burst Hop")]
    [Tooltip("每组连跳次数（=1 时退化为单跳，可作回退开关）")]
    [SerializeField] private int _jumpsPerBurst = 3;

    [Tooltip("每次跳跃高度衰减系数（0~1，越小越矮）")]
    [SerializeField] private float _hopHeightDecay = 0.8f;

    [Tooltip("落地后到下一跳的间隔衰减系数（0~1，越小越快）")]
    [SerializeField] private float _hopIntervalDecay = 0.7f;

    [Tooltip("第一次落地后到下一次起跳的基础间隔（秒）")]
    [SerializeField] private float _baseHopInterval = 0.15f;

    [Header("Pant")]
    [Tooltip("组间喘息最短时长（秒）")]
    [SerializeField] private float _pantDurationMin = 0.8f;

    [Tooltip("组间喘息最长时长（秒）")]
    [SerializeField] private float _pantDurationMax = 1.5f;

    [Header("Search")]
    [Tooltip("威胁值高于此值时触发搜索行为")]
    [SerializeField] private float _searchThreatThreshold = 30f;

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

        _root = BuildTree();
    }

    /// <summary>
    /// 组装青蛙行为树。感知数据统一从 Blackboard 读取。
    /// </summary>
    private BTNode BuildTree()
    {
        Blackboard bb = _frog.Board;

        // 分支0：被吞噬/受击眩晕中 → 原地僵直（最高优先级）
        BTNode stunnedBranch = new BTSequence(
            new BTCondition(() => bb.IsStunned),
            new BTStunnedAction(_frog));

        // 分支1：移动指令已下达但几乎没位移（卡死）→ 脱困（仅次于眩晕）
        BTNode unstickBranch = new BTSequence(
            new BTCondition(() => _frog.IsStuck),
            new BTUnstickAction(_frog));

        // 分支2：玩家可见且威胁足够高 → 逃跑
        _fleeAction = new BTFleeAction(_frog);
        BTNode fleeBranch = new BTSequence(
            new BTCondition(() => bb.IsThreatUrgent),
            _fleeAction);

        // 分支3：玩家不可见但有威胁记忆 → 前往最后已知位置搜索
        BTNode searchBranch = new BTSequence(
            new BTCondition(() => bb.ShouldSearch && bb.ThreatLevel >= _searchThreatThreshold),
            new BTSearchAction(_frog, 1f, 1.2f)
        );

        // 分支4：检测到食物 → 捕食
        BTNode pounceBranch = new BTSequence(
            new BTCondition(() => bb.IsFoodDetected),
            new BTChaseAction(_frog, 1.8f, 0.6f));

        // 分支5：默认觅食循环 → 连跳一组 → 喘息 → 循环
        // 连跳组内高度递减/间隔缩短（BTBurstHopAction），一组跳完喘息短暂停顿（BTPantAction）
        BTNode forageBranch = new BTSequence(
            new BTBurstHopAction(_frog, GetForageDirection, 1f,
                _jumpsPerBurst, _hopHeightDecay, _hopIntervalDecay, _baseHopInterval),
            new BTPantAction(_frog, _pantDurationMin, _pantDurationMax));

        return new BTSelector(stunnedBranch, unstickBranch, fleeBranch, searchBranch, pounceBranch, forageBranch);
    }

    /// <summary>
    /// 觅食跳跃方向：70% 概率朝出生点，30% 纯随机。
    /// </summary>
    private float GetForageDirection()
    {
        Vector2 toSpawn = _frog.SpawnPosition - (Vector2)_frog.transform.position;
        float biasDirection = Mathf.Sign(toSpawn.x);
        return Random.value < 0.7f ? biasDirection : (Random.value < 0.5f ? 1f : -1f);
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
    /// 逃跑时额外输出紧迫度/威胁值/地形信息。
    /// </summary>
    private void LogStateChange(BTNode.State result)
    {
        Blackboard bb = _frog.Board;

        string branch = bb.IsStunned ? "Stunned眩晕"
            : _frog.IsStuck ? "Unstick脱困"
            : bb.IsThreatUrgent ? "Flee逃跑"
            : bb.ShouldSearch ? "Search搜索"
            : bb.IsFoodDetected ? "Pounce捕食"
            : "Forage觅食";

        bool grounded = _frog.IsGrounded;

        if (branch == _lastBranch && result == _lastResult && grounded == _lastGrounded)
            return;

        string extra = "";
        if (branch == "Flee逃跑" && _fleeAction != null)
            extra = $" 紧迫度[{_fleeAction.UrgencyLevel}] 威胁值[{bb.ThreatLevel:F0}]" +
                    $" 墙[{(bb.IsWallAhead ? "有" : "无")}] 沟[{(bb.IsGapAhead ? "有" : "无")}]";

        Debug.Log($"{gameObject.name} BT: 分支[{branch}] 结果[{result}] 着地[{grounded}]{extra}");
        _lastBranch = branch;
        _lastResult = result;
        _lastGrounded = grounded;
    }
}
