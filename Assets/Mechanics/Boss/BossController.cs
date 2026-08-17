using System;
using System.Collections;
using UnityEngine;

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
/// - BOSS 受击伤害入口 TakeDamage(int) 已就绪，具体伤害来源（玩家直击/蜂巢连锁爆炸）由组员后续对接；
/// - 蜂巢被毁默认不直接伤害 BOSS（_segmentDamagePerHive=0），如需"一蜂巢=一段血"可配置为 1。
/// </summary>
public class BossController : MonoBehaviour, IHazardSource
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

    [Header("出场/退去")]
    [Tooltip("出场起点偏移（相对站位）：从场边滑入。零向量=原地出场")]
    [SerializeField] private Vector2 _enterOffset = new Vector2(10f, 0f);
    [Tooltip("出场滑入耗时（秒）")]
    [SerializeField] private float _enterDuration = 1.2f;
    [Tooltip("退去目标位移（相对站位）")]
    [SerializeField] private Vector2 _exitOffset = new Vector2(8f, 0f);
    [Tooltip("退去滑出耗时（秒）")]
    [SerializeField] private float _exitDuration = 1.5f;
    [Tooltip("BOSS 战摄像机正交尺寸（默认 5 → 6.5 拉远）")]
    [SerializeField] private float _arenaCameraSize = 6.5f;
    [Tooltip("战斗结束后恢复的摄像机正交尺寸")]
    [SerializeField] private float _idleCameraSize = 5f;

    [Header("血条 3 段")]
    [Tooltip("血条段数（3 段，打空触发狂暴升级）")]
    [SerializeField] private int _segmentCount = 3;
    [Tooltip("每段血条血量")]
    [SerializeField] private int _segmentHealth = 3;
    [Tooltip("Enrage3 最终阶段残余血量（打空 → PendingVictory）")]
    [SerializeField] private int _finalHealth = 2;
    [Tooltip("Pending 硬直时长（秒）：段打空/胜利前的停顿")]
    [SerializeField] private float _pendingDuration = 1.2f;
    [Tooltip("冷静半径（米）：PendingEnrage 期间玩家离开此距离 → 转 PendingCalm（脱战退档回血）")]
    [SerializeField] private float _calmRadius = 12f;
    [Tooltip("冷静退档是否恢复打空的段（true=脱战回血，false=仅退档不回血）")]
    [SerializeField] private bool _calmRestoresSegment = true;
    [Tooltip("胜利触发-蜂巢全毁")]
    [SerializeField] private bool _winOnAllHives = true;
    [Tooltip("胜利触发-血量打空")]
    [SerializeField] private bool _winOnHpDepleted = true;
    [Tooltip("每毁一个蜂巢对 BOSS 造成的段伤害（0=蜂巢不伤 BOSS，走 _winOnAllHives 胜利；1=一蜂巢一段血）")]
    [SerializeField] private int _segmentDamagePerHive = 0;

    [Header("攻击调度")]
    [Tooltip("攻击冷却（秒）：Normal")]
    [SerializeField] private float _cooldownNormal = 3f;
    [Tooltip("攻击冷却（秒）：Enrage1")]
    [SerializeField] private float _cooldownEnrage1 = 2.5f;
    [Tooltip("攻击冷却（秒）：Enrage2")]
    [SerializeField] private float _cooldownEnrage2 = 2f;
    [Tooltip("攻击冷却（秒）：Enrage3")]
    [SerializeField] private float _cooldownEnrage3 = 1.4f;
    [Tooltip("攻击命中玩家的伤害值（PlayerHP.TakeDamage）")]
    [SerializeField] private int _attackDamage = 1;

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
    private int _hiveDestroyedCount;

    private Transform _player;
    private PlayerHP _playerHP;
    private BossAttack _currentAttack;
    private Coroutine _attackLoop;
    private Coroutine _movementRoutine;
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

    // ---- 感知层契约（组员 B 的 EnvironmentMonitor 读取）----
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

    private void Awake()
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

        if (_attacks == null || _attacks.Length == 0)
            _attacks = GetComponentsInChildren<BossAttack>(true);
        if (_hives == null || _hives.Length == 0)
            _hives = FindObjectsByType<Hive>(FindObjectsSortMode.None);
        if (_telegraph == null)
            _telegraph = GetComponentInChildren<BossTelegraph>(true);
        if (_camera == null)
            _camera = FindObjectOfType<CameraController>();
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

    private void Update()
    {
        if (!_isActive) return;

        if (_pending != PendingType.None)
            UpdatePending();
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

        // 摄像机拉远（5 → 6.5 平滑）
        if (_camera != null) _camera.EnterBossArena(_arenaCameraSize);

        // 出场滑入
        if (_movementRoutine != null) StopCoroutine(_movementRoutine);
        _movementRoutine = StartCoroutine(EnterRoutine());

        // 攻击调度
        _attackLoop = StartCoroutine(AttackLoop());

        Debug.Log($"[BossController] {name} 进入场地，阶段={_phase}", this);
    }

    private IEnumerator EnterRoutine()
    {
        if (_enterOffset.sqrMagnitude > 0.0001f)
        {
            Vector2 start = _arenaPosition + _enterOffset;
            transform.position = start;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _enterDuration);
                transform.position = Vector2.Lerp(start, _arenaPosition, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.position = _arenaPosition;
        }
        _movementRoutine = null;
    }

    /// <summary>胜利退去：滑出场地 → 摄像机恢复 → 广播胜利（OnDefeated + MockEventCenter）。</summary>
    private IEnumerator DefeatSequence()
    {
        if (_attackLoop != null) { StopCoroutine(_attackLoop); _attackLoop = null; }

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
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, _exitDuration);
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

        if (_hives != null)
        {
            foreach (Hive hive in _hives)
                if (hive != null)
                    hive.ResetHive();
        }
        NotifyHPChanged();
    }

    /// <summary>
    /// BOSS 受击（伤害来源：玩家直击/蜂巢连锁爆炸等，由组员对接）。
    /// 段内伤害累计到当前段；段打空 → PendingEnrage（硬直后狂暴升级）。
    /// Enrage3 最终阶段结算残余血量，打空 → PendingVictory。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (!_isActive || _phase == BossPhase.Defeated || _pending != PendingType.None)
            return;
        if (amount <= 0) return;

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
        if (_remainingSegments <= 0)
        {
            // 最后一段打空：进入 Enrage3 最终阶段（用残余血量）
            _inFinalPhase = true;
            _finalHPRemaining = Mathf.Max(1, _finalHealth);
            SetPhase(BossPhase.Enrage3);
            NotifyHPChanged();
            BeginPending(PendingType.Enrage);
            return;
        }

        _currentSegmentHP = _segmentMax;
        SetPhase(PhaseFromSegments(_remainingSegments));
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

        // 全毁 → 胜利
        if (_winOnAllHives && RemainingActiveHives() <= 0)
            BeginPending(PendingType.Victory);
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
                // 组员 A 未交付攻击实现时兜底等待，避免空转刷屏
                yield return new WaitForSeconds(1f);
                continue;
            }

            _currentAttack = attack;
            BossAttackContext ctx = new BossAttackContext
            {
                player = _player,
                snakeHead = _snakeHead,
                snakeTail = _snakeTail,
                telegraph = _telegraph,
                onHit = HandleAttackHit,
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

    private float GetCooldown() => _phase switch
    {
        BossPhase.Enrage1 => _cooldownEnrage1,
        BossPhase.Enrage2 => _cooldownEnrage2,
        BossPhase.Enrage3 => _cooldownEnrage3,
        _ => _cooldownNormal,
    };

    // ===================== 落点伤害对接（onHit 回调）=====================

    /// <summary>
    /// 攻击命中回调（BossAttack.Execute 判定命中点后调用）：
    /// 判定命中玩家 → PlayerHP.TakeDamage；命中蜂巢 → Hive.TakeHit；互不耽误（独立结算）。
    /// </summary>
    private void HandleAttackHit(Vector2 hitPoint)
    {
        DamagePlayerIfHit(hitPoint);
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

            // 落点在蜂巢 2×2 判定盒内 → 受击
            if (Vector2.Distance(hitPoint, hive.transform.position) <= _hiveHitRadius)
                hive.TakeHit(hitPoint);
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
