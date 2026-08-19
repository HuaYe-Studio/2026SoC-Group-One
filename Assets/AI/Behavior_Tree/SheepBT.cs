using System.Collections.Generic;
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

    [Header("群体巡游 (Boids)")]
    [Tooltip("启用 Boids 三力修正漫游方向（聚合/对齐/分离）。\n关闭时完全退化为普通巡游（回退开关）")]
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

    [Tooltip("修正强度上限（0~1：三力相对导航方向的最大混合比例，防三力盖过导航）")]
    [SerializeField, Range(0f, 1f)] private float _flockMaxSteer = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private SheepAI _sheep;
    private RevengeBehavior _revenge;
    private BTNode _root;
    private AnimalHurtFeedback _hurtFeedback; // 受伤反馈组件（受伤时弹跳+位移）

    // 群体巡游（Boids）：成员标识 + 邻居查询缓冲（复用，非分配）
    private FlockMember _flockMember;
    private readonly List<FlockMember> _neighbors = new List<FlockMember>(16);

    // 领地：个体领地（独立动物各有自己的领地，避免同类挤在一起）
    private string _territoryKey;
    private bool _territoryReady;

    // 调试用：只在分支/结果变化时输出日志
    private string _lastBranch;
    private BTNode.State _lastResult;

    private void Awake()
    {
        _sheep = GetComponent<SheepAI>();
        _revenge = GetComponent<RevengeBehavior>();
        _flockMember = GetComponent<FlockMember>();

        // Boids 兜底：prefab 未挂 FlockMember 时运行时补挂（Awake 阶段添加，当帧完成群注册）
        if (_flockMember == null && _enableBoids)
            _flockMember = gameObject.AddComponent<FlockMember>();

        // 个体差异：确保存在 AnimalStats（随机基础数值 + 强度分），未挂则运行时补挂
        AnimalStats stats = GetComponent<AnimalStats>();
        if (stats == null)
            stats = gameObject.AddComponent<AnimalStats>();

        // 受伤反馈：确保存在 AnimalHurtFeedback（受伤时弹跳+位移+无敌），未挂则运行时补挂
        _hurtFeedback = GetComponent<AnimalHurtFeedback>();
        if (_hurtFeedback == null)
            _hurtFeedback = gameObject.AddComponent<AnimalHurtFeedback>();

        // MC 式软推开：同类重叠时沿最短穿透轴物理推开（仅水平，横板标准），未挂则运行时补挂
        if (GetComponent<AnimalSoftPush>() == null)
            gameObject.AddComponent<AnimalSoftPush>();

        // 注册个体领地：每只羊独立领地（以实例 ID 为 key），半径随强度分映射
        _territoryKey = gameObject.GetInstanceID().ToString();
        TerritoryManager.Register(_territoryKey, _sheep.SpawnPosition, AnimalRegion.RegionType.Generic, isShared: false, strength: stats.Strength);

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

        // 分支2：受伤反馈 → 弹跳 + 位移（眩晕/脱困之后、复仇之前，受伤瞬间抢占）
        BTNode hurtBranch = new BTSequence(
            new BTCondition(() => _hurtFeedback.IsHurting),
            new BTHurtFeedbackAction(_sheep, _hurtFeedback));

        // 分支3：复仇状态且复仇目标存活 → 追猎 + 冲撞攻击（目标抽象：攻击者，不限玩家）
        BTNode revengeBranch = new BTSequence(
            new BTCondition(() => _revenge.IsRevenge && _revenge.RevengeTarget != null),
            new BTChargeAction(_sheep,
                hasTarget: () => _revenge.IsRevenge && _revenge.RevengeTarget != null,
                targetPos: () => (Vector2)_revenge.RevengeTarget.transform.position));

        // 分支3：默认自由巡游（中立：不逃跑、不搜索），方向经 Boids 三力修正、中心为个体领地
        BTNode wanderBranch = new BTWanderAction(_sheep, _wanderRange, default, ApplyFlockSteering, GetTerritoryCenter);

        return new BTSelector(stunnedBranch, unstickBranch, hurtBranch, revengeBranch, wanderBranch);
    }

    /// <summary>巡游中心：优先用个体领地中心，未分配时回退出生点。</summary>
    private Vector2 GetTerritoryCenter()
    {
        Territory t = TerritoryManager.Get(_territoryKey);
        return t != null ? t.Center : _sheep.SpawnPosition;
    }

    /// <summary>
    /// Boids 三力修正：仅作用于漫游分支，聚合/对齐/分离叠加在漫游导航方向上（只做偏移修正，
    /// 路点导航仍由漫游节点负责）。未挂 FlockMember、功能关闭或无邻居时原样返回。
    /// </summary>
    private float ApplyFlockSteering(float direction)
    {
        if (!_enableBoids || _flockMember == null)
            return direction;

        if (FlockManager.GetNeighbors(_flockMember, _flockNeighborRadius, _neighbors) == 0)
            return direction;

        return BoidsSteering.ApplyHorizontal(direction, _flockMember.transform.position, _flockMember.Velocity,
            _neighbors, _flockSeparationRadius, _separationWeight, _alignmentWeight,
            _cohesionWeight, _flockMaxSteer);
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

        // 首帧：所有动物 Awake 完成后统一分配领地
        if (!_territoryReady)
        {
            TerritoryManager.EnsureAssigned();
            _territoryReady = true;
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
