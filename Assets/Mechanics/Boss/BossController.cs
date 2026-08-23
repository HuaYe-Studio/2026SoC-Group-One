using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// BOSS 动画状态枚举：驱动 Animator 整数参数（BOSS_AnimState），与动物 AnimState 模式一致。
/// 预留接口：BossController 在关键时机自动调用 SetAnimState；攻击实现/表现层可直接复用。
/// 动画师只需在 Animator 上建立同名整数参数并挂对应状态（0=Idle 1=Enter 2=Attack 3=Enrage 4=Defeated）。
/// </summary>
public enum BossAnimState
{
    Idle = 0,     // 待机/常态
    Enter = 1,    // 出场滑入
    Attack = 2,   // 通用攻击（拍击/撕咬/横扫共用，细分状态可后续扩展枚举）
    Enrage = 3,   // 狂暴（阶段变化时触发，动画师可做变身闪红/膨胀）
    Defeated = 4, // 被击败/退去
}

/// <summary>
/// 蛇王 BOSS 控制器：状态机（5 状态）+ 3 段血条 + 攻击调度 + 威胁源注册 + 出场/退去。
///
/// 状态机：Normal → Enrage1 → Enrage2 → Enrage3 → Defeated。
/// 三标志：PendingEnrage（段打空后的狂暴硬直）/ PendingCalm（狂暴前玩家脱战，冷静退档回血）/
///         PendingVictory（全灭蜂巢或血量打空后的胜利停顿）。
/// 血条 3 段：每段打空进入 PendingEnrage 硬直，随后狂暴升级；狂暴越高攻击冷却越短、威胁半径/强度越高。
/// 攻击调度：从挂载的 BossAttack 实现按权重选择（A40/B20/C40），按阶段冷却执行；
///           命中回调统一走 HandleAttackHit —— 判定命中玩家 → PlayerHP.TakeDamage，命中蜂巢 → Hive.TakeHit，互不耽误。
/// 威胁源：实现 IHazardSource（BOSS 本体接触伤害），并暴露 ThreatTransform / ThreatRadius / ThreatLevel / CurrentPhase
///           供感知层（EnvironmentMonitor）写入动物 Blackboard 的 BOSS 维度（IsBossDetected/BossDistance/...）。
/// 出场/退去：EnterArena()（滑入出场 + 血条广播 + 摄像机拉远）；退去（滑出 + 场地开放 + 摄像机恢复）。
///
/// 设计约定（需团队确认）：
/// - 胜利条件默认「破坏全部蜂巢」（_winOnAllHives）或「Enrage3 残余血量打空」（_winOnHpDepleted），两者独立；
/// - BOSS 受击伤害入口 TakeDamage(int) 已就绪，具体伤害来源（玩家直击/蜂巢连锁爆炸等）由后续系统对接；
/// - 蜂巢被毁默认不直接伤害 BOSS（_segmentDamagePerHive=0），如需"一蜂巢=一段血"可配置为 1。
/// </summary>
public class BossController : MonoBehaviour, IHazardSource, IAttackTarget
{
    /// <summary>内部待决状态：由三公开标志（PendingEnrage/PendingCalm/PendingVictory）驱动。</summary>
    public enum PendingType { None, Enrage, Calm, Victory }

    [Header("挂载引用")]
    [Tooltip("蛇头 Transform（撕咬 C 出场点；同时作为威胁感知锚点 ThreatTransform）")]
    [SerializeField] private Transform _snakeHead;
    [Tooltip("蛇尾 Transform（拍击 A / 横扫 B 出场点）")]
    [SerializeField] private Transform _snakeTail;
    [Tooltip("红框预警组件（攻击预警用；未配置则尝试在子物体查找）")]
    [SerializeField] private BossTelegraph _telegraph;
    [Tooltip("三种攻击实现（A拍击/B全屏/C撕咬，子类 BossAttack）。留空则自动从子物体查找")]
    [SerializeField] private BossAttack[] _attacks;
    [Tooltip("场上蜂巢（破坏目标）。留空则自动 FindObjectsByType")]
    [SerializeField] private Hive[] _hives;
    [Tooltip("摄像机控制器（入场拉远/退去恢复）。留空则自动查找")]
    [SerializeField] private CameraController _camera;
    [Tooltip("BOSS 动画组件（Animator 参数 BOSS_AnimState）。留空则自动在自身/子物体查找")]
    [SerializeField] private Animator _animator;

    [Header("出场/退去")]
    [Tooltip("出场起点偏移（相对站位）：BOSS 从「站位+此偏移」处伸出至站位。\n竖直蛇推荐负 Y（从地下向上伸出）；正值=从右侧伸入，负值=从左侧伸入。零向量=原地出场")]
    [SerializeField] private Vector2 _enterOffset = new Vector2(0f, -8f);
    [Tooltip("出场伸出速度（米/秒）")]
    [SerializeField] private float _enterSpeed = 6f;
    [Tooltip("退去目标偏移（相对站位）：BOSS 往「站位+此偏移」方向缩回。\n竖直蛇推荐负 Y（缩回地下）；正值=向右退去，负值=向左退去")]
    [SerializeField] private Vector2 _exitOffset = new Vector2(0f, -8f);
    [Tooltip("退去缩回速度（米/秒）")]
    [SerializeField] private float _exitSpeed = 5f;
    [Tooltip("BOSS 战摄像机正交尺寸（默认 5 → 6.5 拉远）")]
    [SerializeField] private float _arenaCameraSize = 6.5f;
    [Tooltip("战斗结束后恢复的摄像机正交尺寸")]
    [SerializeField] private float _idleCameraSize = 5f;

    [Header("自动激活（兜底）")]
    [Tooltip("玩家进入该距离后自动激活 BOSS 战（未配置 BossArenaTrigger 时的兜底）")]
    [SerializeField] private float _autoActivateRadius = 8f;

    [Header("血条 3 段")]
    [Tooltip("血条段数（3 段，打空触发狂暴升级）")]
    [SerializeField] private int _segmentCount = 3;
    [Tooltip("每段血条血量")]
    [SerializeField] private int _segmentHealth = 3;
    [Tooltip("Enrage3 最终阶段残余血量（打空 → PendingVictory）")]
    [SerializeField] private int _finalHealth = 2;
    [Header("狂暴持续时间")]
    [Tooltip("Enrage1 持续时间（秒）：狂暴结束后自动恢复普通状态")]
    [SerializeField] private float _enrageDuration1 = 5f;
    [Tooltip("Enrage2 持续时间（秒）：狂暴结束后自动恢复普通状态")]
    [SerializeField] private float _enrageDuration2 = 7f;
    [Tooltip("Enrage3 持续时间（秒）：最终狂暴。0=不自动恢复（靠残余血打空胜利）")]
    [SerializeField] private float _enrageDuration3 = 0f;
    [Tooltip("Pending 硬直时长（秒）：段打空/胜利前的停顿")]
    [SerializeField] private float _pendingDuration = 1.2f;
    [Tooltip("冷静半径（米）：PendingEnrage 期间玩家离开此距离 → 转 PendingCalm（脱战退档回血）")]
    [SerializeField] private float _calmRadius = 12f;
    [Tooltip("冷静退档是否恢复打空的段（true=脱战回血，false=仅退档不回血）")]
    [SerializeField] private bool _calmRestoresSegment = true;
    [Tooltip("胜利触发-蜂巢全毁")]
    [SerializeField] private bool _winOnAllHives = true;
    [Tooltip("胜利所需破坏的蜂巢数（0=全部蜂巢；如 3=毁 3 个蜂巢即胜利）")]
    [SerializeField] private int _hivesToWin = 0;
    [Tooltip("胜利触发-血量打空")]
    [SerializeField] private bool _winOnHpDepleted = true;
    [Tooltip("每毁一个蜂巢对 BOSS 造成的段伤害（1=一蜂巢一段血；0=蜂巢不伤 BOSS，只走 _winOnAllHives 胜利）")]
    [SerializeField] private int _segmentDamagePerHive = 1;

    [Header("攻击调度")]
    [Tooltip("攻击冷却区间（秒）：Normal")]
    [SerializeField] private Vector2 _cooldownNormal = new Vector2(3f, 4f);
    [Tooltip("攻击冷却区间（秒）：Enrage1")]
    [SerializeField] private Vector2 _cooldownEnrage1 = new Vector2(2f, 2.5f);
    [Tooltip("攻击冷却区间（秒）：Enrage2")]
    [SerializeField] private Vector2 _cooldownEnrage2 = new Vector2(1.8f, 2.2f);
    [Tooltip("攻击冷却区间（秒）：Enrage3")]
    [SerializeField] private Vector2 _cooldownEnrage3 = new Vector2(1.5f, 2f);
    [Tooltip("攻击命中玩家的伤害值（PlayerHP.TakeDamage）")]
    [SerializeField] private int _attackDamage = 1;

    [Header("移动追击")]
    [Tooltip("开启战斗期追击：BOSS 朝玩家水平移动（除出场/退去滑行外的主动移动方式）")]
    [SerializeField] private bool _enableChase = true;
    [Tooltip("追击速度（米/秒）")]
    [SerializeField] private float _chaseSpeed = 2f;
    [Tooltip("贴近距离（米）：与玩家距离小于此值停止追击（给攻击预警/判定腾出空间）")]
    [SerializeField] private float _chaseStopDistance = 2f;
    [Tooltip("最大追击距离（米）：超出此距离不追（防止追出场外）")]
    [SerializeField] private float _chaseMaxDistance = 20f;

    [Header("台阶攀爬")]
    [Tooltip("开启台阶攀爬：追击时自动检测前方台阶并上下爬升，越过台阶地形")]
    [SerializeField] private bool _enableStepClimb = true;
    [Tooltip("台阶检测地面层（台阶/地形所在层，通常为 Ground）")]
    [SerializeField] private LayerMask _groundLayer = 1 << 3;
    [Tooltip("身体底部相对根节点的 Y 偏移（米）：探测脚底高度，用于判断台阶顶面")]
    [SerializeField] private float _footOffsetY = 0f;
    [Tooltip("前方台阶探测距离（米）：从脚底向前打射线找台阶立面")]
    [SerializeField] private float _stepProbeDistance = 1f;
    [Tooltip("可攀爬最大台阶高度（米）：高于此值的台阶不爬")]
    [SerializeField] private float _maxStepHeight = 0.6f;
    [Tooltip("攀爬/下降垂直速度（米/秒）")]
    [SerializeField] private float _stepClimbSpeed = 3f;

    [Header("威胁源（IHazardSource + 感知契约）")]
    [Tooltip("接触伤害值（BOSS 本体触碰动物/玩家，供 IHazardSource 消费方使用）")]
    [SerializeField] private int _contactDamage = 1;
    [Tooltip("接触击退力")]
    [SerializeField] private Vector2 _contactKnockback = new Vector2(3f, 4f);
    [Tooltip("接触是否即死")]
    [SerializeField] private bool _contactInstantKill = false;
    [Tooltip("感知半径（常态）：感知层据此检测动物/玩家感知 BOSS")]
    [SerializeField] private float _threatRadiusNormal = 8f;
    [Tooltip("感知半径（狂暴时增大）")]
    [SerializeField] private float _threatRadiusEnrage = 14f;
    [Tooltip("各阶段威胁强度 0~100（Normal/Enrage1/Enrage2/Enrage3），狂暴越高越危险")]
    [SerializeField] private float[] _phaseThreatLevels = { 60f, 75f, 90f, 100f };
    [Tooltip("落点命中蜂巢判定半径（米）：对应蜂巢 2×2 判定盒半宽")]
    [SerializeField] private float _hiveHitRadius = 1.1f;

    // ---- 运行时状态 ----
    private bool _isActive;
    private BossPhase _phase = BossPhase.Normal;
    private int _segmentMax;
    private int _currentSegmentHP;
    private int _remainingSegments;
    private int _finalHPRemaining;
    private bool _inFinalPhase;      // Enrage3：改用残余血量
    private PendingType _pending = PendingType.None;
    private float _pendingUntil;
    private float _enrageEndTime;   // 狂暴结束时间（0=不自动恢复，用于固定时长狂暴）
    private int _hiveDestroyedCount;

    private Transform _player;
    private PlayerHP _playerHP;
    private BossAttack _currentAttack;
    private Coroutine _attackLoop;
    private Coroutine _movementRoutine;
    private Coroutine _chaseRoutine;   // 移动追击协程（战斗期朝玩家水平移动）
    private Rigidbody2D _rb;           // 追击用刚体（Kinematic 优先；未挂则直接改 Transform）
    private Vector2 _arenaPosition;  // 站位（出场滑入/退去滑出的基准点）

    // ---- 对外事件契约（UI 血条/文字提示/音效订阅）----
    /// <summary>阶段变化（Normal→Enrage1→...→Defeated）。</summary>
    public event Action<BossPhase> OnPhaseChanged;
    /// <summary>被击败（退去完成后）。</summary>
    public event Action OnDefeated;
    /// <summary>血量变化（段内/段打空/残余血量变化时触发，UI 血条刷新）。</summary>
    public event Action OnHPChanged;
    // ---- 三标志（待决状态，供表现层/调试查询）----
    public bool PendingEnrage => _pending == PendingType.Enrage;
    public bool PendingCalm => _pending == PendingType.Calm;
    public bool PendingVictory => _pending == PendingType.Victory;

    public bool IsActive => _isActive;
    public BossPhase CurrentPhase => _phase;
    public int RemainingSegments => _remainingSegments;
    public int CurrentSegmentHP => _currentSegmentHP;
    public int SegmentMax => _segmentMax;

    // ---- 感知层契约（EnvironmentMonitor 读取）----
    /// <summary>BOSS 位置锚点（感知层检测基准）。</summary>
    public Transform ThreatTransform => _snakeHead != null ? _snakeHead : transform;
    /// <summary>感知半径（狂暴时增大）。</summary>
    public float ThreatRadius => _phase == BossPhase.Normal ? _threatRadiusNormal : _threatRadiusEnrage;
    /// <summary>威胁强度（狂暴时提升，写入 Blackboard.BossThreatLevel 供仲裁）。</summary>
    public float ThreatLevel =>
        _phaseThreatLevels != null && _phaseThreatLevels.Length > (int)_phase
            ? _phaseThreatLevels[(int)_phase]
            : 100f;

    // ---- IHazardSource（BOSS 本体接触伤害；落点伤害走 onHit 回调）----
    public bool IsInstantKill => _contactInstantKill;
    public int Damage => _contactDamage;
    public Vector2 Knockback => _contactKnockback;

    // ---- IAttackTarget（蜜蜂等攻击单位追踪/受创/击败，面向接口编程）----
    /// <summary>目标位置锚点（蛇头，蜜蜂追踪基准）。</summary>
    public Vector2 Position => ThreatTransform.position;
    /// <summary>目标是否存活：已激活且未被击败。</summary>
    public bool IsAlive => _isActive && _phase != BossPhase.Defeated;
    /// <summary>受创到阈值（段血打空）→ 蜜蜂等攻击单位驱散。</summary>
    public event Action OnWeakened;

    // ---- 动画预留接口（Animator 参数 BOSS_AnimState，见 BossAnimState 枚举）----
    /// <summary>BOSS Animator（供攻击实现/表现层直接取用；未配置时返回 null）。</summary>
    public Animator BossAnimator => _animator;

    /// <summary>
    /// 设置 BOSS 动画状态（写 Animator 整数参数 BOSS_AnimState）。
    /// 未挂 Animator 时静默忽略，不影响逻辑运行。动画师可直接用此方法驱动任意动画。
    /// </summary>
    public void SetAnimState(BossAnimState state)
    {
        if (_animator == null) return;
        _animator.SetInteger(BossAnimParam, (int)state);
    }

    private const string BossAnimParam = "BOSS_AnimState";

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (_player == null)
        {
            // 项目里多个子物体（形态）都带 "Player" 标签，FindGameObjectWithTag 会返回非根节点（形态子物体），
            // 而 PlayerHP 挂在根节点（与 PlayerController 同物体）上。与 HeavyObject 一致，用 PlayerController 定位根节点。
            PlayerController pc = FindObjectOfType<PlayerController>();
            if (pc != null)
            {
                _player = pc.transform;
                _playerHP = pc.GetComponent<PlayerHP>();
            }
        }

        // 数组为空或全为 null（prefab 序列化常见 `[null]`）时自动查找，避免空引用元素顶掉自动发现
        if (IsNullOrAllNull(_attacks))
            _attacks = GetComponentsInChildren<BossAttack>(true);
        if (IsNullOrAllNull(_hives))
            _hives = FindObjectsByType<Hive>(FindObjectsSortMode.None);
        if (_telegraph == null)
            _telegraph = GetComponentInChildren<BossTelegraph>(true);
        if (_camera == null)
            _camera = FindObjectOfType<CameraController>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);
        if (_snakeHead == null) _snakeHead = transform;
        if (_snakeTail == null) _snakeTail = transform;

        if (_hives != null)
        {
            foreach (Hive hive in _hives)
                if (hive != null)
                    hive.OnDestroyed += OnHiveDestroyed;
        }
    }

    private void OnDestroy()
    {
        if (_hives == null) return;
        foreach (Hive hive in _hives)
            if (hive != null)
                hive.OnDestroyed -= OnHiveDestroyed;
    }

    private void OnEnable()
    {
        MockEventCenter.OnPlayerRespawn += OnPlayerRespawn;
    }

    private void OnDisable()
    {
        MockEventCenter.OnPlayerRespawn -= OnPlayerRespawn;
    }

    /// <summary>
    /// 玩家死亡复活回调：死亡复活不重载场景，只传送玩家，因此 BOSS 战状态（血量/追击仇恨/蜂巢/蜜蜂）
    /// 都不会自动重置。这里在复活时把 BOSS 战整体复位到「未激活」状态：
    /// 仇恨（追击/攻击）关闭、血量/阶段复位、BOSS 回到出场站位、蜂巢恢复并重新生成守护蜜蜂。
    /// 玩家重新靠近 BOSS 后再由 TryAutoActivate / BossArenaTrigger 重新开战。
    /// </summary>
    private void OnPlayerRespawn()
    {
        if (!_isActive) return; // BOSS 未激活，无需复位

        ResetForRespawn();
    }

    /// <summary>把 BOSS 战整体复位到未激活状态（玩家复活 / 需要重开 BOSS 战时调用）。</summary>
    public void ResetForRespawn()
    {
        // 1. 停止所有战斗协程，关闭仇恨（追击/攻击）
        if (_attackLoop != null) { StopCoroutine(_attackLoop); _attackLoop = null; }
        if (_chaseRoutine != null) { StopCoroutine(_chaseRoutine); _chaseRoutine = null; }
        if (_movementRoutine != null) { StopCoroutine(_movementRoutine); _movementRoutine = null; }
        _currentAttack = null;

        _isActive = false;
        _pending = PendingType.None;
        _pendingUntil = 0f;
        _enrageEndTime = 0f;

        // 2. 血量/段/蜂巢自身状态复位
        ResetFight();

        // 3. 阶段回到 Normal（UI 血条/阶段显示复位）
        SetPhase(BossPhase.Normal);

        // 4. 回到出场站位（追击可能把 BOSS 带离站位）
        transform.position = _arenaPosition;

        // 5. 隐藏预警
        if (_telegraph != null) _telegraph.Hide();

        // 6. 蜂巢重新生成守护蜜蜂（蜂巢被毁后蜜蜂已飞散回池，这里重新补上）
        if (_hives != null)
        {
            foreach (Hive hive in _hives)
                if (hive != null)
                    hive.RespawnBees();
        }

        Debug.Log($"[BossController] {name} 玩家复活，BOSS 战已复位（仇恨关闭、血量复位、蜂巢/蜜蜂恢复）", this);
    }

    /// <summary>数组为空、或所有元素都是 null（prefab 里 `[null]` 序列化的典型形态）。</summary>
    private static bool IsNullOrAllNull<T>(T[] array) where T : class
    {
        if (array == null || array.Length == 0) return true;
        for (int i = 0; i < array.Length; i++)
            if (array[i] != null)
                return false;
        return true;
    }

    private void Update()
    {
        if (!_isActive)
        {
            // 未激活时：玩家进入警戒范围 → 自动激活 BOSS 战（兜底，保证大蛇战能触发）
            TryAutoActivate();
            return;
        }

        if (_pending != PendingType.None)
            UpdatePending();

        UpdateEnrageTimer();
    }

    private void TryAutoActivate()
    {
        if (_player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _player = playerGo.transform;
                _playerHP = playerGo.GetComponent<PlayerHP>();
            }
        }
        if (_player == null) return;
        if (Vector2.Distance(_player.position, transform.position) <= _autoActivateRadius)
        {
            Debug.Log($"[BossController] {name} 玩家进入警戒范围，自动激活 BOSS 战", this);
            EnterArena();
        }
    }

    /// <summary>固定时长狂暴计时：到点自动恢复普通状态（Enrage3 最终狂暴由 duration=0 禁用）。</summary>
    private void UpdateEnrageTimer()
    {
        if (_enrageEndTime <= 0f || _phase == BossPhase.Defeated) return;
        if (Time.time < _enrageEndTime) return;

        _enrageEndTime = 0f;
        SetPhase(BossPhase.Normal);
        Debug.Log($"[BossController] {name} 狂暴结束，恢复普通状态", this);
    }

    /// <summary>按狂暴阶段取持续时间并启动计时（duration=0 表示不自动恢复）。</summary>
    private void StartEnrageTimer(BossPhase phase)
    {
        float duration = phase switch
        {
            BossPhase.Enrage1 => _enrageDuration1,
            BossPhase.Enrage2 => _enrageDuration2,
            BossPhase.Enrage3 => _enrageDuration3,
            _ => 0f,
        };
        _enrageEndTime = duration > 0f ? Time.time + duration : 0f;
    }

    // ===================== 出场 / 退去 =====================

    /// <summary>
    /// 进入 BOSS 战（地编触发器调用）：重置战斗状态 → 滑入出场 → 血条广播 → 摄像机拉远 → 启动攻击循环。
    /// </summary>
    public void EnterArena()
    {
        if (_isActive) return;

        _isActive = true;
        _arenaPosition = transform.position;
        ResetFight();
        SetPhase(BossPhase.Normal);

        if (_telegraph != null) _telegraph.Hide();

        // 出场动画（滑入期间）
        SetAnimState(BossAnimState.Enter);

        // 摄像机拉远（5 → 6.5 平滑）
        if (_camera != null) _camera.EnterBossArena(_arenaCameraSize);

        // 出场滑入
        if (_movementRoutine != null) StopCoroutine(_movementRoutine);
        _movementRoutine = StartCoroutine(EnterRoutine());

        // 攻击调度
        _attackLoop = StartCoroutine(AttackLoop());

        // 移动追击（战斗期朝玩家水平移动）
        if (_enableChase)
            _chaseRoutine = StartCoroutine(ChasePlayer());

        Debug.Log($"[BossController] {name} 进入场地，阶段={_phase}", this);
    }

    private IEnumerator EnterRoutine()
    {
        if (_enterOffset.sqrMagnitude > 0.0001f)
        {
            Vector2 start = _arenaPosition + _enterOffset;
            transform.position = start;
            float duration = _enterOffset.magnitude / Mathf.Max(0.01f, _enterSpeed);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                transform.position = Vector2.Lerp(start, _arenaPosition, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.position = _arenaPosition;
        }
        // 出场伸出结束 → 待机
        SetAnimState(BossAnimState.Idle);
        _movementRoutine = null;
    }

    /// <summary>胜利退去：滑出场地 → 摄像机恢复 → 广播胜利（OnDefeated + MockEventCenter）。</summary>
    private IEnumerator DefeatSequence()
    {
        if (_attackLoop != null) { StopCoroutine(_attackLoop); _attackLoop = null; }
        if (_chaseRoutine != null) { StopCoroutine(_chaseRoutine); _chaseRoutine = null; }

        SetPhase(BossPhase.Defeated);

        // 胜利停顿（表现窗口：UI 胜利提示/音效）
        yield return new WaitForSeconds(_pendingDuration);

        // 退去滑出
        if (_movementRoutine != null) StopCoroutine(_movementRoutine);
        _movementRoutine = StartCoroutine(ExitRoutine());
        yield return _movementRoutine;
    }

    private IEnumerator ExitRoutine()
    {
        // 摄像机恢复（6.5 → 5）
        if (_camera != null) _camera.ExitBossArena(_idleCameraSize);

        Vector2 start = transform.position;
        Vector2 target = start + _exitOffset;
        float duration = _exitOffset.magnitude / Mathf.Max(0.01f, _exitSpeed);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        _isActive = false;
        _movementRoutine = null;

        // 胜利广播（先事件后隐藏，避免协程被禁用中断）
        OnDefeated?.Invoke();
        MockEventCenter.TriggerBossDefeated();
        Debug.Log($"[BossController] {name} 已击败，退去并开放场地", this);

        gameObject.SetActive(false);
    }

    // ===================== 移动追击 =====================

    /// <summary>
    /// 战斗期移动追击：每帧朝玩家水平移动（只动 x，保留重力/物理 y）。
    /// 追击条件：激活 + 非击败 + 玩家存在 + 距离在（贴近距离, 最大追击距离）区间内。
    /// 贴近距离内停止追击，给攻击预警/判定腾出空间；超出最大距离不追，防止追出场外。
    /// </summary>
    private IEnumerator ChasePlayer()
    {
        while (_isActive && _phase != BossPhase.Defeated)
        {
            if (_player != null)
            {
                float dist = Vector2.Distance(transform.position, _player.position);
                if (dist > _chaseStopDistance && dist <= _chaseMaxDistance)
                {
                    float dir = Mathf.Sign(_player.position.x - transform.position.x);
                    // 水平朝玩家逼近 + 台阶自动爬升/下降（越过台阶地形）
                    float climbY = ComputeStepClimbY(dir);
                    MoveBoss(new Vector3(dir * _chaseSpeed * Time.deltaTime, climbY, 0f));
                }
            }
            yield return null;
        }
        _chaseRoutine = null;
    }

    /// <summary>
    /// 位移 BOSS：有 Rigidbody2D 时用 MovePosition（走物理，可碰撞/不穿墙），否则直接改 Transform。
    /// </summary>
    private void MoveBoss(Vector3 step)
    {
        if (_rb != null)
            _rb.MovePosition(_rb.position + (Vector2)step);
        else
            transform.position += step;
    }

    /// <summary>
    /// 台阶自动爬升/下降：朝移动方向探测前方台阶，返回本帧的垂直位移。
    /// - 前方有立面（台阶墙）且台阶顶面高度在最大爬升高度内 → 返回正 Y（向上爬）
    /// - 前方无立面且脚下地面低于当前脚底（下台阶/悬崖）→ 返回负 Y（向下）
    /// 垂直位移按 _stepClimbSpeed 限速，逐帧平滑过渡；无台阶时返回 0。
    /// </summary>
    private float ComputeStepClimbY(float moveDir)
    {
        if (!_enableStepClimb || Mathf.Abs(moveDir) < 0.0001f)
            return 0f;

        Vector2 foot = (Vector2)transform.position + Vector2.up * _footOffsetY;
        Vector2 dir = Vector2.right * moveDir;

        // 1) 水平射线：探测前方台阶立面
        RaycastHit2D wall = Physics2D.Raycast(foot, dir, _stepProbeDistance, _groundLayer);
        if (wall.collider != null)
        {
            // 2) 从台阶顶端略上方一点向下打射线，求台阶顶面高度
            Vector2 topOrigin = wall.point + Vector2.up * (_maxStepHeight + 0.2f);
            RaycastHit2D top = Physics2D.Raycast(topOrigin, Vector2.down, _maxStepHeight + 0.4f, _groundLayer);
            if (top.collider != null)
            {
                float rise = top.point.y - foot.y;
                if (rise > 0.01f && rise <= _maxStepHeight)
                    return Mathf.Min(rise, _stepClimbSpeed * Time.deltaTime); // 向上爬升
            }
        }
        else
        {
            // 3) 前方无立面：检测下台阶（前方脚底地面低于当前脚底）
            Vector2 aheadFoot = foot + dir * _stepProbeDistance;
            RaycastHit2D ground = Physics2D.Raycast(aheadFoot, Vector2.down, _maxStepHeight + 0.4f, _groundLayer);
            if (ground.collider != null)
            {
                float drop = foot.y - ground.point.y;
                if (drop > 0.01f && drop <= _maxStepHeight)
                    return -Mathf.Min(drop, _stepClimbSpeed * Time.deltaTime); // 向下下降
            }
        }
        return 0f;
    }

    // ===================== 血条 / 阶段 =====================

    private void ResetFight()
    {
        _remainingSegments = Mathf.Max(1, _segmentCount);
        _segmentMax = Mathf.Max(1, _segmentHealth);
        _currentSegmentHP = _segmentMax;
        _finalHPRemaining = Mathf.Max(1, _finalHealth);
        _inFinalPhase = false;
        _hiveDestroyedCount = 0;
        _pending = PendingType.None;
        _pendingUntil = 0f;
        _enrageEndTime = 0f;

        if (_hives != null)
        {
            foreach (Hive hive in _hives)
                if (hive != null)
                    hive.ResetHive();
        }
        NotifyHPChanged();
    }

    /// <summary>
    /// BOSS 受击（伤害来源：玩家直击/蜂巢连锁爆炸等，由后续系统对接）。
    /// 段内伤害累计到当前段；段打空 → PendingEnrage（硬直后狂暴升级）。
    /// Enrage3 最终阶段结算残余血量，打空 → PendingVictory。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (!_isActive || _phase == BossPhase.Defeated || _pending != PendingType.None)
            return;
        if (amount <= 0) return;

        Debug.Log($"[BossController] {name} 受击 -{amount}（段内={_currentSegmentHP}/{_segmentMax}，剩余段={_remainingSegments}，终段={_inFinalPhase}）", this);

        if (_inFinalPhase)
        {
            // Enrage3 最终阶段：残余血量
            _finalHPRemaining = Mathf.Max(0, _finalHPRemaining - amount);
            NotifyHPChanged();
            if (_finalHPRemaining <= 0 && _winOnHpDepleted)
                BeginPending(PendingType.Victory);
            return;
        }

        _currentSegmentHP -= amount;
        NotifyHPChanged();
        if (_currentSegmentHP > 0) return;

        // 段打空 → 扣除段数并进入狂暴硬直
        _remainingSegments = Mathf.Max(0, _remainingSegments - 1);
        OnWeakened?.Invoke();
        if (_remainingSegments <= 0)
        {
            // 最后一段打空：进入 Enrage3 最终阶段（用残余血量）
            _inFinalPhase = true;
            _finalHPRemaining = Mathf.Max(1, _finalHealth);
            SetPhase(BossPhase.Enrage3);
            StartEnrageTimer(BossPhase.Enrage3);
            NotifyHPChanged();
            BeginPending(PendingType.Enrage);
            return;
        }

        _currentSegmentHP = _segmentMax;
        BossPhase enragePhase = PhaseFromSegments(_remainingSegments);
        SetPhase(enragePhase);
        StartEnrageTimer(enragePhase);
        BeginPending(PendingType.Enrage);
    }

    /// <summary>剩余段数 → 阶段（3=Normal，2=Enrage1，1=Enrage2，0=Enrage3）。</summary>
    private BossPhase PhaseFromSegments(int remaining) => remaining switch
    {
        2 => BossPhase.Enrage1,
        1 => BossPhase.Enrage2,
        _ => BossPhase.Enrage3,
    };

    private void SetPhase(BossPhase phase)
    {
        if (_phase == phase) return;
        _phase = phase;
        OnPhaseChanged?.Invoke(_phase);
        MockEventCenter.TriggerBossPhaseChanged(_phase);

        // 动画联动：进入狂暴（Enrage1-3）播狂暴变身；被击败播退场
        if (phase == BossPhase.Defeated)
            SetAnimState(BossAnimState.Defeated);
        else if (phase != BossPhase.Normal)
            SetAnimState(BossAnimState.Enrage);
    }

    private void NotifyHPChanged()
    {
        OnHPChanged?.Invoke();
    }

    // ===================== Pending 时序 =====================

    private void BeginPending(PendingType type)
    {
        _pending = type;
        _pendingUntil = Time.time + _pendingDuration;
    }

    private void UpdatePending()
    {
        if (Time.time < _pendingUntil) return;

        switch (_pending)
        {
            case PendingType.Enrage:
                // 硬直期间玩家离开冷静半径 → 转 PendingCalm（脱战退档回血），否则正式狂暴升级
                if (_player != null &&
                    Vector2.Distance(_player.position, transform.position) > _calmRadius)
                {
                    BeginPending(PendingType.Calm);
                    return;
                }
                _pending = PendingType.None;
                break;

            case PendingType.Calm:
                ResolveCalm();
                _pending = PendingType.None;
                break;

            case PendingType.Victory:
                _pending = PendingType.None;
                StartCoroutine(DefeatSequence());
                break;
        }
        _pendingUntil = 0f;
    }

    /// <summary>冷静退档：恢复打空的段（可选）并回退阶段，给玩家喘息窗口。</summary>
    private void ResolveCalm()
    {
        if (_calmRestoresSegment && !_inFinalPhase)
        {
            _remainingSegments = Mathf.Min(_remainingSegments + 1, Mathf.Max(1, _segmentCount));
            _currentSegmentHP = _segmentMax;
            SetPhase(PhaseFromSegments(_remainingSegments));
            NotifyHPChanged();
        }
        else if (_inFinalPhase)
        {
            // Enrage3 不退回：段数已尽，仅重置残余（脱战喘息窗口）
            _finalHPRemaining = Mathf.Max(1, _finalHealth);
            NotifyHPChanged();
        }
        Debug.Log($"[BossController] {name} 冷静退档，剩余段={_remainingSegments}", this);
    }

    // ===================== 蜂巢 =====================

    private void OnHiveDestroyed(Hive hive)
    {
        _hiveDestroyedCount++;
        Debug.Log($"[BossController] {name} 蜂巢 {hive.HiveIndex} 被破坏（{_hiveDestroyedCount}/{(_hives?.Length ?? 0)}）", this);

        // 可配置：蜂巢被毁对 BOSS 造成段伤害
        if (_segmentDamagePerHive > 0)
            TakeDamage(_segmentDamagePerHive * _segmentMax);

        // 全毁（或达到 _hivesToWin 指定数量）→ 胜利
        bool winReached = _hivesToWin > 0
            ? _hiveDestroyedCount >= _hivesToWin
            : RemainingActiveHives() <= 0;
        if (_winOnAllHives && winReached)
        {
            BeginPending(PendingType.Victory);
            LogBossStatus($"蜂巢全毁 → 进入胜利时序");
            return;
        }

        // 设计（大蛇BOSS战 v1.0）：每破坏一个蜂巢 → 进入下一档狂暴（Normal→Enrage1→Enrage2→Enrage3），
        // 狂暴持续结束后由 StartEnrageTimer/UpdateEnrageTimer 自动恢复普通状态。
        if (_isActive && _pending == PendingType.None)
        {
            BossPhase next = _hiveDestroyedCount switch
            {
                1 => BossPhase.Enrage1,
                2 => BossPhase.Enrage2,
                _ => BossPhase.Enrage3,
            };
            if (next != _phase)
            {
                SetPhase(next);
                StartEnrageTimer(next);
                BeginPending(PendingType.Enrage);
            }
        }

        LogBossStatus($"蜂巢 {hive.HiveIndex} 被破坏后");
    }

    /// <summary>
    /// 输出 BOSS 当前完整状态日志（蜂巢被破坏等关键节点后调用，排查"BOSS 无敌/不胜利/不掉血"）。
    /// </summary>
    private void LogBossStatus(string context)
    {
        Debug.Log(
            $"[BossController][状态] {context} 激活={_isActive} 阶段={_phase} 待决={_pending}(到{_pendingUntil:F1}s) " +
            $"剩余蜂巢={RemainingActiveHives()}/{(_hives?.Length ?? 0)} 已毁={_hiveDestroyedCount} 目标={(_hivesToWin > 0 ? _hivesToWin.ToString() : "全部")} " +
            $"段={_remainingSegments} 段内HP={_currentSegmentHP}/{_segmentMax} " +
            $"终段={_inFinalPhase} 终段余血={_finalHPRemaining} 目标存活={(_player != null)} " +
            $"胜利开关[蜂巢全毁={_winOnAllHives}/血打空={_winOnHpDepleted}] 蜂巢段伤={_segmentDamagePerHive}",
            this);
    }

    private int RemainingActiveHives()
    {
        if (_hives == null) return 0;
        int count = 0;
        for (int i = 0; i < _hives.Length; i++)
            if (_hives[i] != null && !_hives[i].IsDestroyed)
                count++;
        return count;
    }

    // ===================== 攻击调度 =====================

    private IEnumerator AttackLoop()
    {
        while (_isActive && _phase != BossPhase.Defeated)
        {
            // Pending 硬直期间暂停攻击
            while (_pending != PendingType.None)
                yield return null;

            // 按阶段冷却
            yield return new WaitForSeconds(GetCooldown());

            if (!_isActive || _phase == BossPhase.Defeated || _pending != PendingType.None)
                continue;

            BossAttack attack = PickAttack();
            if (attack == null)
            {
                // 未挂载攻击实现时兜底等待，避免空转刷屏
                yield return new WaitForSeconds(1f);
                continue;
            }

            // 攻击动画（细分攻击可后续扩展枚举，如 AttackSlam/AttackBite）
            SetAnimState(BossAnimState.Attack);

            _currentAttack = attack;
            BossAttackContext ctx = new BossAttackContext
            {
                player = _player,
                snakeHead = _snakeHead,
                snakeTail = _snakeTail,
                telegraph = _telegraph,
                onHit = HandleAttackHit,
                enraged = _phase != BossPhase.Normal,
            };

            yield return StartCoroutine(attack.Execute(ctx));
            _currentAttack = null;
        }
        _attackLoop = null;
    }

    /// <summary>按概率权重选择攻击（A40 / B20 / C40 由各攻击 Probability 归一化）。</summary>
    private BossAttack PickAttack()
    {
        if (_attacks == null || _attacks.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < _attacks.Length; i++)
            if (_attacks[i] != null)
                total += Mathf.Max(0f, _attacks[i].Probability);

        if (total <= 0f) return _attacks[0];

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < _attacks.Length; i++)
        {
            BossAttack attack = _attacks[i];
            if (attack == null) continue;
            roll -= Mathf.Max(0f, attack.Probability);
            if (roll <= 0f) return attack;
        }
        return _attacks[0];
    }

    private float GetCooldown()
    {
        Vector2 range = _phase switch
        {
            BossPhase.Enrage1 => _cooldownEnrage1,
            BossPhase.Enrage2 => _cooldownEnrage2,
            BossPhase.Enrage3 => _cooldownEnrage3,
            _ => _cooldownNormal,
        };
        return UnityEngine.Random.Range(range.x, range.y);
    }

    // ===================== 落点伤害对接（onHit 回调）=====================

    /// <summary>
    /// 攻击命中回调（BossAttack.Execute 判定命中点后调用）：
    /// 判定命中玩家 → PlayerHP.TakeDamage；命中蜂巢 → Hive.TakeHit；互不耽误（独立结算）。
    /// </summary>
    private void HandleAttackHit(Vector2 hitPoint)
    {
        string attackName = _currentAttack != null ? _currentAttack.GetType().Name : "null";
        Debug.Log($"[BossController] {name} 攻击命中 攻击={attackName} 落点=({hitPoint.x:F1},{hitPoint.y:F1}) 可破巢={(_currentAttack != null && _currentAttack.CanDestroyHive)}", this);

        DamagePlayerIfHit(hitPoint);

        // 只有可破坏蜂巢的攻击（A拍击/C撕咬）才结算蜂巢；B横扫全屏不破巢
        if (_currentAttack != null && !_currentAttack.CanDestroyHive)
            return;

        DamageHivesIfHit(hitPoint);
    }

    private void DamagePlayerIfHit(Vector2 hitPoint)
    {
        if (_player == null) return;

        // 用当前攻击的判定盒近似（半盒 + 玩家体型余量）
        Vector2 boxSize = _currentAttack != null ? _currentAttack.HitboxSize : Vector2.one * 1.5f;
        float radius = Mathf.Max(boxSize.x, boxSize.y) * 0.5f + 0.4f;
        if (Vector2.Distance(hitPoint, _player.position) > radius) return;

        if (_playerHP == null)
            _playerHP = _player.GetComponent<PlayerHP>();
        if (_playerHP != null)
            _playerHP.TakeDamage(_attackDamage);
    }

    private void DamageHivesIfHit(Vector2 hitPoint)
    {
        if (_hives == null) return;

        for (int i = 0; i < _hives.Length; i++)
        {
            Hive hive = _hives[i];
            if (hive == null || hive.IsDestroyed) continue;

            // 落点在蜂巢判定盒内 → 受击
            float dist = Vector2.Distance(hitPoint, hive.transform.position);
            if (dist <= _hiveHitRadius)
            {
                Debug.Log($"[BossController] 落点命中蜂巢#{hive.HiveIndex}（距离={dist:F2}m ≤ {_hiveHitRadius}m）", this);
                hive.TakeHit(hitPoint);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 感知半径（威胁源范围）：常态/狂暴两档
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, _threatRadiusNormal);
        Gizmos.color = new Color(1f, 0f, 0f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, _threatRadiusEnrage);

        // 冷静半径（PendingEnrage 脱战判定）
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, _calmRadius);
    }
#endif
}
