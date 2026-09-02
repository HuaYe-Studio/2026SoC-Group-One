using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 青蛙行为树：挂载并启用后驱动青蛙AI。
/// 优先级从高到低：受伤反馈 > BOSS逃跑 > 逃跑（玩家）> 搜索（威胁记忆）> 捕食 > 觅食循环（连跳一组 → 喘息）。
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
    /// <summary>本组件加载的行为树 JSON 名（与 Resources.Load 共用同一常量，编辑器据此反查绑定）。</summary>
    public static string TreeAssetName => "Frog";

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

    [Header("群体分离 (Boids)")]
    [Tooltip("启用同类分离：起跳方向朝远离同类偏移，避免青蛙扎堆。\n青蛙为离散跳跃，只做分离、不做对齐/聚合。关闭即退化为原行为")]
    [SerializeField] private bool _enableSeparation = true;

    [Tooltip("邻居查询半径（米）：只与同群（FlockId 相同）的同类结群")]
    [SerializeField] private float _flockNeighborRadius = 4f;

    [Tooltip("分离半径（米）：小于此距离的同类产生排斥")]
    [SerializeField] private float _flockSeparationRadius = 1.2f;

    [Tooltip("分离力权重（越高越抗拒扎堆）")]
    [SerializeField] private float _separationWeight = 0.8f;

    [Tooltip("修正强度上限（0~1：分离相对导航方向的最大混合比例）")]
    [SerializeField, Range(0f, 1f)] private float _flockMaxSteer = 0.7f;

    [Header("Forage 障碍绕行")]
    [Tooltip("起跳前朝目标方向探测前方障碍的层（地面/尖刺等）。0=不探测（退化为无绕行）")]
    [SerializeField] private LayerMask _forageObstacleMask;

    [Tooltip("前方障碍探测距离（米）：朝领地方向此距离内有障碍就换方向跳，避免撞墙反复卡死")]
    [SerializeField] private float _forageProbeDistance = 2.5f;

    [Tooltip("探测盒高度（米）：覆盖青蛙体型，防止尖刺太小被 Raycast 穿透漏检")]
    [SerializeField] private float _forageProbeHeight = 1.0f;

    [Tooltip("危险物 Tag 列表（如尖刺 Spike）：即使不在障碍层上，探测到也一律绕行")]
    [SerializeField] private string[] _hazardTags = { "Spike" };

    [Tooltip("朝领地中心跳跃的偏向概率（0~1）。0.5=一半机会朝领地，其余自由随机；越大越恋家")]
    [SerializeField, Range(0f, 1f)] private float _territoryBiasChance = 0.5f;

    [Header("伤害源记忆回避")]
    [Tooltip("软→硬递进阈值：记忆期内连续受伤达到此次数后，从'软偏置'升级为'硬禁止'（绝不朝伤害源方向跳）")]
    [SerializeField] private int _hazardHardThreshold = 2;

    [Tooltip("软偏置改道概率（0~1）：软阶段朝伤害源方向跳时，以此概率改为反向（趋势更小）")]
    [SerializeField, Range(0f, 1f)] private float _hazardAvoidChance = 0.7f;

    [Tooltip("软偏置距离缩放（0~1）：软阶段仍朝伤害源方向跳时，跳跃距离的缩放系数（距离更短）")]
    [SerializeField, Range(0f, 1f)] private float _hazardSoftDistanceScale = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private FrogAI _frog;
    private BTNode _root;
    private BTFleeAction _fleeAction; // 持有引用以便调试时读取内部状态
    private BTBossFleeAction _bossFleeAction; // BOSS 逃跑（优先级高于逃离玩家）
    private AnimalHurtFeedback _hurtFeedback; // 受伤反馈组件（受伤时弹跳+位移）

    // 群体分离（Boids）：成员标识 + 邻居查询缓冲（复用，非分配）
    private FlockMember _flockMember;
    private readonly List<FlockMember> _neighbors = new List<FlockMember>(16);

    // 领地：个体领地（每只青蛙独立领地，觅食方向朝领地中心）
    private string _territoryKey;
    private bool _territoryReady;

    // 调试用：记录上次日志的分支/结果/着地，只在变化时输出
    private string _lastBranch;
    private BTNode.State _lastResult;
    private bool _lastGrounded;

    private void OnDestroy()
    {
        // 阶段 0.3：注销本棵树（对象销毁时从注册表移除，避免悬空引用）
        BTTreeRegistry.Unregister(this);
    }

    private void Awake()
    {
        _frog = GetComponent<FrogAI>();
        _flockMember = GetComponent<FlockMember>();

        // 分离兜底：prefab 未挂 FlockMember 时运行时补挂（Awake 阶段添加，当帧完成群注册）
        if (_flockMember == null && _enableSeparation)
            _flockMember = gameObject.AddComponent<FlockMember>();

        // 个体差异：确保存在 AnimalStats（随机基础数值 + 强度分），未挂则运行时补挂
        AnimalStats stats = GetComponent<AnimalStats>();
        if (stats == null)
            stats = gameObject.AddComponent<AnimalStats>();

        // 受伤反馈：确保存在 AnimalHurtFeedback（受伤时弹跳+位移+无敌），未挂则运行时补挂
        _hurtFeedback = GetComponent<AnimalHurtFeedback>();
        if (_hurtFeedback == null)
            _hurtFeedback = gameObject.AddComponent<AnimalHurtFeedback>();
        _hurtFeedback.SetMaxHealth(0); // 青蛙：禁用扣血（受伤不扣血、不死亡），保留无敌/记忆/弹开/闪烁

        // MC 式软推开：同类重叠时沿最短穿透轴物理推开（仅水平，横板标准），未挂则运行时补挂
        if (GetComponent<AnimalSoftPush>() == null)
            gameObject.AddComponent<AnimalSoftPush>();

        // 注册个体领地：每只青蛙独立领地（以实例 ID 为 key），半径随强度分映射
        _territoryKey = gameObject.GetInstanceID().ToString();
        TerritoryManager.Register(_territoryKey, _frog.SpawnPosition, AnimalRegion.RegionType.Generic, isShared: false, strength: stats.Strength);

        _root = BuildTree();

        // 阶段 0.3：向注册表登记本棵行为树（调试器/可视化据此发现树结构）
        BTTreeRegistry.Register(gameObject.name, _root, this);
    }

    /// <summary>
    /// 从 JSON 资产组装青蛙行为树（阶段 4.3 数据驱动）。
    /// 树结构/分支顺序/节点参数走 Assets/Resources/BTTrees/Frog.json；
    /// 逻辑叶子（条件/动作委托）由 ResolveLeaf 按名解析。JSON 缺失或解析失败时回退代码版。
    /// </summary>
    private BTNode BuildTree()
    {
        TextAsset asset = Resources.Load<TextAsset>("BTTrees/" + TreeAssetName);
        if (asset == null)
        {
            Debug.LogError("[FrogBT] 未找到 JSON 树资产 Resources/BTTrees/Frog，回退代码组装");
            return BuildTreeLegacy();
        }

        BTNode root = BTLayoutParser.Load(asset.text, new BTContext(this), ResolveLeaf);
        if (root == null)
        {
            Debug.LogError("[FrogBT] JSON 树解析失败，回退代码组装");
            return BuildTreeLegacy();
        }
        return root;
    }

    /// <summary>JSON 逻辑叶子解析器：按 name 返回对应条件/动作节点（结构在 JSON，逻辑委托在代码）。</summary>
    private BTNode ResolveLeaf(string type, string name, IBTContext ctx)
    {
        // 优先查通用叶子目录（BossUrgent/ThreatUrgent/FoodDetected 等与本地等价）
        BTNode fromCatalog = BTLeafCatalog.Create(name, ctx);
        if (fromCatalog != null) return fromCatalog;

        Blackboard bb = _frog.Board;
        switch (name)
        {
            case "BossUrgent": return new BTCondition(IsBossThreatUrgent);
            case "ThreatUrgent": return new BTCondition(() => bb.IsThreatUrgent);
            case "ShouldSearch": return new BTCondition(() => bb.ShouldSearch && bb.ThreatLevel >= _searchThreatThreshold);
            case "FoodDetected": return new BTCondition(() => bb.IsFoodDetected);
            case "BossFlee": return _bossFleeAction = new BTBossFleeAction(_frog);
            case "Flee": return _fleeAction = new BTFleeAction(_frog);
            case "Search": return new BTSearchAction(_frog, 1f, 1.2f);
            case "Pounce": return new BTChaseAction(_frog, 1.8f, 0.6f);
            case "ForageBurst":
                return new BTBurstHopAction(_frog, GetForageDirection, 1f,
                    _jumpsPerBurst, _hopHeightDecay, _hopIntervalDecay, _baseHopInterval,
                    directionSteer: ApplyFrogSeparation,
                    speedScale: GetHazardSpeedScale);
            case "Pant": return new BTPantAction(_frog, _pantDurationMin, _pantDurationMax);
            default: return null; // 组合/装饰等结构节点交还工厂
        }
    }

    /// <summary>
    /// 代码组装版（JSON 资产缺失/解析失败时的回退）。
    /// 感知数据统一从 Blackboard 读取。
    /// </summary>
    private BTNode BuildTreeLegacy()
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

        // 分支2：受伤反馈 → 弹跳 + 位移（眩晕/脱困之后、逃跑之前，受伤瞬间抢占，位移完成自动恢复）
        BTNode hurtBranch = new BTSequence(
            new BTCondition(() => _hurtFeedback.IsHurting),
            new BTHurtFeedbackAction(_frog, _hurtFeedback));

        // 分支2.5：检测到 BOSS（紧迫威胁）→ 逃离 BOSS。
        // 优先级高于"逃离玩家"（四档仲裁：场景伤害 > BOSS > 玩家），置于玩家逃跑分支之前。
        _bossFleeAction = new BTBossFleeAction(_frog);
        BTNode bossEscapeBranch = new BTSequence(
            new BTCondition(IsBossThreatUrgent),
            _bossFleeAction);

        // 分支3：玩家可见且威胁足够高 → 逃跑
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
        // 起跳方向经同类分离修正，避免青蛙扎堆
        BTNode forageBranch = new BTSequence(
            new BTBurstHopAction(_frog, GetForageDirection, 1f,
                _jumpsPerBurst, _hopHeightDecay, _hopIntervalDecay, _baseHopInterval,
                directionSteer: ApplyFrogSeparation,
                speedScale: GetHazardSpeedScale),
            new BTPantAction(_frog, _pantDurationMin, _pantDurationMax));

        return new BTSelector(stunnedBranch, unstickBranch, hurtBranch, bossEscapeBranch, fleeBranch, searchBranch, pounceBranch, forageBranch);
    }

    /// <summary>BOSS 是否构成紧迫威胁（刷新迟滞仲裁后判定）。</summary>
    private bool IsBossThreatUrgent()
    {
        _frog.Board.RefreshBossUrgent();
        return _frog.Board.IsBossUrgent;
    }

    /// <summary>
    /// 同类分离修正：仅作用于觅食连跳的起跳方向，把"朝导航方向跳"微调为"朝远离扎堆同类方向跳"。
    /// 青蛙为离散跳跃，无持续速度，故只做分离（对齐/聚合无意义）。功能关闭或无邻居时原样返回。
    /// </summary>
    private float ApplyFrogSeparation(float direction)
    {
        if (!_enableSeparation || _flockMember == null)
            return direction;

        if (FlockManager.GetNeighbors(_flockMember, _flockNeighborRadius, _neighbors) == 0)
            return direction;

        return BoidsSteering.ApplyHorizontal(direction, _flockMember.transform.position, _flockMember.Velocity,
            _neighbors, _flockSeparationRadius, _separationWeight, 0f, 0f, _flockMaxSteer);
    }

    /// <summary>
    /// 觅食跳跃方向：50% 概率朝领地大致方向，50% 随机。
    /// 领地只提供"方向偏好"，不是"必须到达的目标"——不强制到达、不强行绕行。
    /// 仅在"选中方向被障碍/危险物挡住"时做软修正（换反方向或原地垂直跳），
    /// 避免"朝墙/尖刺硬跳"触发防卡死。青蛙是机会主义觅食者，领地只是参考。
    /// </summary>
    private float GetForageDirection()
    {
        Vector2 toCenter = GetTerritoryCenter() - (Vector2)_frog.transform.position;
        // 站在领地中心正上方时 Sign(0)=0 会让"朝领地"退化成原地垂直跳（领地中心=出生点，开局即触发），
        // 中心附近（|dx|<0.3）直接随机方向，避免奇点。
        float biasDirection = Mathf.Abs(toCenter.x) > 0.3f
            ? Mathf.Sign(toCenter.x)
            : (Random.value < 0.5f ? 1f : -1f);

        // 50% 概率朝领地大致方向，50% 随机——领地只是"参考"，不是"目标"
        float direction = Random.value < _territoryBiasChance ? biasDirection : (Random.value < 0.5f ? 1f : -1f);

        // 软绕行：目标方向被障碍/危险物挡住时换反方向；两侧皆挡则原地垂直跳（0）。
        // 只做方向修正，不强制到达领地，避免恢复"路径被挡还硬跳"导致的卡死。
        if (IsDirectionBlocked(direction))
        {
            if (!IsDirectionBlocked(-direction))
                direction = -direction;
            else
                direction = 0f;
        }

        // 伤害源记忆回避（软→硬递进）
        direction = ApplyHazardAvoidance(direction);

        return direction;
    }

    /// <summary>
    /// 伤害源记忆回避（软→硬递进）：
    /// - 记忆无效或无方向（原地垂直跳）→ 原样返回；
    /// - 硬阶段（记忆期内连续受伤 ≥ 阈值）→ 强制反向，绝不朝伤害源方向跳；
    /// - 软阶段 → 朝伤害源方向时按概率反向（趋势更小），距离由 GetHazardSpeedScale 缩短。
    /// </summary>
    private float ApplyHazardAvoidance(float direction)
    {
        if (direction == 0f || !_frog.Board.HasHazardMemory)
            return direction;

        float towardSign = Mathf.Sign(_frog.Board.LastHazardPosition.x - _frog.transform.position.x);
        if (Mathf.Sign(direction) != towardSign)
            return direction;   // 不是朝伤害源方向，无需回避

        if (_frog.Board.HazardHitCount >= _hazardHardThreshold)
            return -direction;  // 硬：强制反向

        if (Random.value < _hazardAvoidChance)
            return -direction;  // 软：概率改道，降低朝伤害源方向的趋势

        return direction;       // 软：仍朝伤害源方向，距离缩短
    }

    /// <summary>
    /// 按最终跳跃方向计算距离缩放：软阶段朝伤害源方向跳时缩短距离（更短），硬阶段已反向故正常。
    /// </summary>
    private float GetHazardSpeedScale(float direction)
    {
        if (direction == 0f || !_frog.Board.HasHazardMemory)
            return 1f;

        float towardSign = Mathf.Sign(_frog.Board.LastHazardPosition.x - _frog.transform.position.x);
        bool towardHazard = Mathf.Sign(direction) == towardSign;
        bool isHard = _frog.Board.HazardHitCount >= _hazardHardThreshold;

        if (towardHazard && !isHard)
            return _hazardSoftDistanceScale;

        return 1f;
    }

    /// <summary>
    /// 朝指定水平方向探测前方是否有障碍。用 OverlapBox（非 Raycast）覆盖体型，略抬高避免地面误判。
    /// 检测三层：配置的障碍层 → 危险物 Tag → 任意非触发器固体（兜底，覆盖"默认层无标签的墙"）。
    /// </summary>
    private bool IsDirectionBlocked(float dirX)
    {
        if (_forageObstacleMask.value == 0 && _hazardTags.Length == 0)
            return false;

        // 探测盒下沿必须明显高于脚底（capsule 半高 ~0.23 → 脚底 ≈ pos.y-0.21）：
        // 抬高 0.55 后下沿 = pos.y+0.05，距脚底 ~0.26，避免落地帧穿透/抖动把"脚下地面"误判成前方障碍
        // （此前抬高 0.3 时下沿 = pos.y-0.2，距脚底仅 ~1cm，落地帧恒命中地面 → 两侧皆挡 → 恒返回 0 原地跳）。
        // 抬高后仍可覆盖：1m 高墙（顶面 ~pos.y+0.79）、尖刺（顶面 ~pos.y+0.3）。
        Vector2 origin = (Vector2)_frog.transform.position + Vector2.up * 0.55f;
        Vector2 center = origin + new Vector2(dirX, 0f) * (_forageProbeDistance * 0.5f);
        Vector2 size = new Vector2(_forageProbeDistance, _forageProbeHeight);

        // 1. 障碍层检测（地面/平台等）
        if (_forageObstacleMask.value != 0)
        {
            Collider2D hit = Physics2D.OverlapBox(center, size, 0f, _forageObstacleMask);
            if (hit != null && !hit.isTrigger)
                return true;
        }

        // 2. 危险物检测（尖刺等）：Tag 匹配即绕行，不受层配置限制
        if (_hazardTags.Length > 0)
        {
            Collider2D hit = Physics2D.OverlapBox(center, size, 0f);
            if (hit != null && !hit.isTrigger)
            {
                for (int i = 0; i < _hazardTags.Length; i++)
                {
                    if (hit.CompareTag(_hazardTags[i]))
                        return true;
                }
            }
        }

        // 3. 兜底：检测任意非触发器固体（排除自身所在 Animal 层），
        //    覆盖"默认层且无 Tag 的墙"等漏配，避免反复撞墙
        int excludeSelfLayer = 1 << _frog.gameObject.layer;
        Collider2D solid = Physics2D.OverlapBox(center, size, 0f, ~excludeSelfLayer);
        if (solid != null && !solid.isTrigger)
            return true;

        return false;
    }

    /// <summary>觅食中心：优先用个体领地中心，未分配时回退出生点。</summary>
    private Vector2 GetTerritoryCenter()
    {
        Territory t = TerritoryManager.Get(_territoryKey);
        return t != null ? t.Center : _frog.SpawnPosition;
    }

    private void Update()
    {
        // 兜底：播放中热重载脚本会清空私有字段，此处检测并重建行为树
        if (_root == null)
        {
            _frog = GetComponent<FrogAI>();
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

    /// <summary>
    /// 只在分支、结果或着地状态发生变化时输出日志，避免刷屏。
    /// 逃跑时额外输出紧迫度/威胁值/地形信息。
    /// </summary>
    private void LogStateChange(BTNode.State result)
    {
        Blackboard bb = _frog.Board;

        string branch = bb.IsStunned ? "Stunned眩晕"
            : _frog.IsStuck ? "Unstick脱困"
            : bb.IsBossUrgent ? "BossEscape逃离BOSS"
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
