using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 蜜蜂：环境常驻生物（蜂巢周围生成，平时绕巢巡游，像鱼群一样是环境的一部分）。
/// 移动采用鱼群同套 A* 寻路（NavGrid2D 避开 ground/障碍 + AnimalRegion 限定活动区域 +
/// FlockMember/BoidsSteering 三力结群），无上浮/下潜需求，全向飞行。
/// 行为三态（由 BeeBT 行为树驱动）：
///   1. 守护 —— A* 绕蜂巢巡游点轮换（默认态，无需任何事件）
///   2. 攻击 —— 蜂巢破坏后 A* 接近目标，贴身蜜刺（固定伤害 + 冷却）
///   3. 飞散 —— 目标受创到阈值/被击败时背离目标飞出屏幕销毁
/// 目标面向接口 IAttackTarget 编程（不依赖具体类）：BOSS/其他单位实现接口即可被攻击。
/// 轻量自检感知：定时扫描警戒范围自动锁定最近目标（无预设目标时兜底），
///   也是扩展口子——未来玩家/动物实现 IAttackTarget 后，蜜蜂无需改代码即可索敌。
/// 伤害对接：蜜刺 → IAttackTarget.TakeDamage（每次固定 _stingDamage，冷却 _stingCooldown）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(FlockMember))]
public class BeeAI : MonoBehaviour
{
    [Header("移动")]
    [Tooltip("飞行速度（米/秒）")]
    [SerializeField] private float _flySpeed = 3f;

    [Header("Boids 蜂群参数（复用 BoidsSteering 三力）")]
    [Tooltip("邻居感知半径（米）：同群蜜蜂互为邻居")]
    [SerializeField] private float _neighborRadius = 2.5f;
    [Tooltip("分离半径（米）：小于此距离排斥（防扎堆）")]
    [SerializeField] private float _separationRadius = 0.7f;
    [SerializeField] private float _separationWeight = 1.4f;
    [SerializeField] private float _alignmentWeight = 0.6f;
    [SerializeField] private float _cohesionWeight = 0.25f;
    [Tooltip("三力最大修正强度（0~1），防 Boids 盖过导航方向")]
    [SerializeField, Range(0f, 1f)] private float _maxSteer = 0.7f;

    [Header("蜜刺攻击")]
    [Tooltip("每次蜜刺对 BOSS 造成的固定伤害（每只独立结算）")]
    [SerializeField] private int _stingDamage = 1;
    [Tooltip("蜜刺冷却（秒）：同一只蜜蜂的两次伤害间隔")]
    [SerializeField] private float _stingCooldown = 1f;
    [Tooltip("蜜刺范围（米）：进入此距离才触发伤害")]
    [SerializeField] private float _stingRange = 1f;

    [Header("行为配置")]
    [Tooltip("守护半径（米）：绕蜂巢随机巡游点的分布半径")]
    [SerializeField] private float _guardRadius = 1.5f;
    [Tooltip("守护巡游点数：全圆贪心分布的候选点（默认 6，建议 ≥ 蜜蜂数且点距≥体宽）")]
    [SerializeField] private int _guardPointCount = 6;
    [Tooltip("巡游点间最小间距（米）：贪心分布时保证点距 ≥ 此值（建议 ≥ 蜜蜂体宽，防点过密）。0 = 不限制")]
    [SerializeField] private float _minPointSeparation = 0.5f;
    [Tooltip("守护巡游点到达判定半径（米）：到达当前点后立即换下一个点")]
    [SerializeField] private float _guardArriveRadius = 0.6f;
    [Tooltip("飞行随机噪声幅度（米/秒量级）：叠加随机扰动让飞行更混乱自然。0 = 纯寻路无抖动")]
    [SerializeField] private float _wanderNoise = 0.4f;
    [Tooltip("巡游点随机抖动幅度（米）：泊松盘采样基础上的额外随机偏移，让每只蜜蜂巡游轨迹更错开。0 = 无额外偏移")]
    [SerializeField] private float _guardPointJitter = 0f;
    [Tooltip("巡游点最低高度相对蜂巢的偏移（米）：保证巡游点在蜂巢上方，不穿地面。蜜蜂调小后可调小此值。0 = 不限制")]
    [SerializeField] private float _guardMinHeightOffset = 0.1f;
    [Tooltip("飞散出屏判定边距（视口 0~1，超出此范围销毁）")]
    [SerializeField] private float _scatterMargin = 0.15f;

    [Header("调试")]
    [Tooltip("调试日志开关：输出寻路结果/巡游停歇/换点/状态切换日志，便于定位蜜蜂卡住问题")]
    [SerializeField] private bool _debugLog = true;

    // ---- 区域限定（A* costAt）：由 Hive 生成时注入，不在 Inspector 逐只配置 ----
    private AnimalRegion _region; // null = 不限制（只受 NavGrid2D 网格边界约束）

    [Header("轻量自检感知（扩展口子：无预设目标时自动索敌）")]
    [Tooltip("扫描间隔（秒）：定时检测警戒范围内的 IAttackTarget")]
    [SerializeField] private float _scanInterval = 0.5f;
    [Tooltip("警戒半径（米）：超过此范围不索敌")]
    [SerializeField] private float _scanRadius = 8f;
    [Tooltip("扫描层位：限定哪些层参与索敌。默认全部层")]
    [SerializeField] private LayerMask _scanLayers = -1;

    private Rigidbody2D _rb;
    private FlockMember _flock;
    private readonly List<FlockMember> _neighbors = new List<FlockMember>();
    private Camera _mainCamera;
    private static readonly Collider2D[] _scanBuffer = new Collider2D[16];

    private Transform _hiveAnchor;      // 守护锚点（蜂巢位置）
    private IAttackTarget _target;      // 攻击目标（接口，不依赖具体类）
    private float _nextStingTime;
    private float _nextScanTime;
    private float _nextStatusLogTime;

    private readonly List<Vector2> _guardPoints = new List<Vector2>(); // 守护候选巡游点（绕蜂巢一圈）
    private int _guardIndex;
    private readonly HashSet<int> _claimedPoints = new HashSet<int>(); // 同族已认领的巡游点（避扎堆）

    // ---- 行为标志（BeeBT 条件查询）----
    private bool _hiveDestroyed;        // 蜂巢已破坏 → 切攻击
    private bool _scatterRequested;     // 目标受创/被击败 → 切飞散

    public bool HiveDestroyed => _hiveDestroyed;
    public bool ShouldScatter => _scatterRequested;
    public bool IsTargetAlive => _target != null && _target.IsAlive;

    /// <summary>调试日志开关（BeeAStarMoveAction 等外部也读取此标志控制日志输出）。</summary>
    public bool DebugLog => _debugLog;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _flock = GetComponent<FlockMember>();
        _mainCamera = Camera.main;
        if (_rb != null) _rb.gravityScale = 0f;

        // 自动补挂 MC 式软推开（蜜蜂全向飞行 → 全向推开），防扎堆穿模
        AnimalSoftPush softPush = GetComponent<AnimalSoftPush>();
        if (softPush == null)
            softPush = gameObject.AddComponent<AnimalSoftPush>();
        softPush.Dimension = AnimalSoftPush.PushDimension.Omnidirectional;
    }

    private void OnDestroy()
    {
        FlockManager.ReleaseClaimedPoint(_flock); // 释放巡游点认领，避免占着不放
        if (_target != null)
        {
            _target.OnWeakened -= OnTargetWeakened;
            _target.OnDefeated -= OnTargetDefeated;
        }
    }

    /// <summary>
    /// 初始化（由 Hive 常驻生成时调用）：设置守护锚点、初始目标与活动区域。
    /// 初始处于守护态（_hiveDestroyed=false），蜂巢被破坏时由 Hive 调 SetHiveDestroyed 切入攻击态。
    /// target 可为空：届时靠轻量自检感知自动索敌（见 ScanForTarget）。
    /// region 由生成者（Hive）统一注入，蜜蜂自身不配置活动范围。
    /// </summary>
    public void Init(Transform hiveAnchor, IAttackTarget target, AnimalRegion region = null)
    {
        _hiveAnchor = hiveAnchor;
        _region = region;
        SetTarget(target);
        _hiveDestroyed = false; // 常驻守护态；破坏后切攻击

        // 出生点防嵌套：若生成位置在障碍/悬空格内，向上抬升到最近可通行格，避免一出生就卡地形
        ResolveSpawnPosition();

        // 每只克隆各自随机生成自己的巡游点（泊松盘），保证个体轨迹各不相同；
        // 认领机制只在索引层面避让，用于初始错开。
        BuildGuardPoints();

        // 初始认领一个未被同族占用的巡游点（同族通信：蜜蜂一出生就错开，降低扎堆）
        if (_guardPoints.Count > 0)
        {
            _guardIndex = PickUnclaimedPoint();
            FlockManager.ClaimPoint(_flock, _guardIndex);
        }
    }

    /// <summary>
    /// 出生点防嵌套：若当前位置在不可通行格（障碍/悬空格），向上逐格抬升到最近可通行格。
    /// 解决蜜蜂在 Square/地形内部生成时，被 ClampToFreeCell 吸到边缘导致穿地/卡顿。
    /// </summary>
    private void ResolveSpawnPosition()
    {
        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null) return;

        Vector2Int cell = grid.WorldToCell(transform.position);
        if (cell.x < 0) return; // 网格外不处理
        if (!grid.IsBlockedFor(cell.x, cell.y, true)) return; // 已在可通行格，无需处理

        // 向上找最近可通行格（蜜蜂是飞行单位，向上抬升比水平移动更自然）
        const int maxLift = 12;
        for (int dy = 1; dy <= maxLift; dy++)
        {
            Vector2Int lifted = new Vector2Int(cell.x, cell.y + dy);
            if (!grid.IsBlockedFor(lifted.x, lifted.y, true))
            {
                Vector2 worldPos = grid.CellToWorld(lifted.x, lifted.y);
                transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
                return;
            }
        }
    }

    /// <summary>蜂巢被破坏 → 切入攻击态（由 Hive 在 TakeHit 破坏时调用）。</summary>
    public void SetHiveDestroyed()
    {
        _hiveDestroyed = true;
        if (_debugLog)
            Debug.Log($"[BeeAI][状态] {name} → 攻击态（蜂巢已破坏，目标={(_target != null ? "有" : "无")}）", this);
    }

    private void Update()
    {
        ScanForTarget();
        LogStatus();
    }

    /// <summary>周期状态报告（每 2 秒）：输出蜜蜂位置/速度/目标点/距离/状态，定位"卡住"时蜜蜂在干嘛。</summary>
    private void LogStatus()
    {
        if (!_debugLog) return;
        if (Time.time < _nextStatusLogTime) return;
        _nextStatusLogTime = Time.time + 2f;

        Vector2 pos = transform.position;
        Vector2 vel = _rb != null ? _rb.velocity : Vector2.zero;
        string targetDesc = "无";
        if (_guardPoints.Count > 0 && _guardIndex < _guardPoints.Count)
        {
            Vector2 gp = _guardPoints[_guardIndex];
            targetDesc = $"#{_guardIndex}({gp.x:F1},{gp.y:F1}) 距={Vector2.Distance(pos, gp):F2}m";
        }
        Debug.Log($"[BeeAI][状态] {name} pos=({pos.x:F1},{pos.y:F1}) vel={vel.magnitude:F2} 巡游点{_guardPoints.Count}个 目标={targetDesc} 巢毁={_hiveDestroyed} 目标活={IsTargetAlive}", this);
    }

    /// <summary>
    /// 轻量自检感知：无有效目标时，定时扫描警戒范围内的 IAttackTarget 并锁定最近的。
    /// 与鱼群的 EnvironmentMonitor 感知不同，蜜蜂不挂 Blackboard，自扫自足；
    /// 这是扩展口子——未来玩家/动物实现 IAttackTarget 后即可被自动索敌。
    /// </summary>
    private void ScanForTarget()
    {
        if (Time.time < _nextScanTime) return;
        _nextScanTime = Time.time + _scanInterval;

        if (_target != null && _target.IsAlive) return; // 已有有效目标，不重复扫描

        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, _scanRadius, _scanBuffer, _scanLayers);
        float bestSqr = float.MaxValue;
        IAttackTarget best = null;
        for (int i = 0; i < hitCount; i++)
        {
            IAttackTarget t = _scanBuffer[i].GetComponent<IAttackTarget>();
            if (t == null || !t.IsAlive) continue;
            float sqr = ((Vector2)_scanBuffer[i].transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        if (best != null && best != _target)
            SetTarget(best);
    }

    /// <summary>设置攻击目标并维护事件订阅（切换目标时先退订旧的）。</summary>
    private void SetTarget(IAttackTarget target)
    {
        if (_target == target) return;

        if (_target != null)
        {
            _target.OnWeakened -= OnTargetWeakened;
            _target.OnDefeated -= OnTargetDefeated;
        }

        _target = target;

        if (_target != null)
        {
            _target.OnWeakened += OnTargetWeakened;
            _target.OnDefeated += OnTargetDefeated;
        }
    }

    private void OnTargetWeakened()
    {
        _scatterRequested = true;
        if (_debugLog)
            Debug.Log($"[BeeAI][状态] {name} → 飞散（目标受创到阈值）", this);
    }

    private void OnTargetDefeated()
    {
        _scatterRequested = true;
        if (_debugLog)
            Debug.Log($"[BeeAI][状态] {name} → 飞散（目标被击败）", this);
    }

    // ===================== 行为原语（BeeBT 调用） =====================

    /// <summary>
    /// 守护巡游目标（BeeAStarMoveAction.targetProvider）：返回当前认领的巡游点。
    /// **这里不做"到达即切换"**——切换由 BeeAStarMoveAction 到达后通过 onArrive 回调
    /// 触发 AdvanceGuardPoint()（否则同帧"切换→立即判断到达新点→Success→再切换"死循环，蜜蜂永久悬停）。
    /// </summary>
    public Vector2 GetNextGuardPoint()
    {
        if (_hiveAnchor == null || _guardPoints.Count == 0)
        {
            if (_debugLog && _hiveAnchor == null)
                Debug.Log($"[BeeAI][守护] {name} 无蜂巢锚点，原地待命", this);
            return transform.position;
        }
        return _guardPoints[_guardIndex];
    }

    /// <summary>
    /// 推进巡游点（BeeAStarMoveAction 到达后回调）：
    /// 优先选"离当前位置最远的未认领点"（飞行距离长、轨迹铺得开、防扎堆）；
    /// 认领点被同族占满时，**必须换一个非当前点**——否则 best 保持当前点，
    /// 下一帧"已到达当前点→Success→再回调→再选当前点"死循环，蜜蜂永久悬停。
    /// </summary>
    public void AdvanceGuardPoint()
    {
        if (_guardPoints.Count == 0) return;
        int old = _guardIndex;
        if (_guardPoints.Count == 1)
        {
            FlockManager.ClaimPoint(_flock, _guardIndex);
            return;
        }

        FlockManager.GetClaimedPoints(_flock, _claimedPoints);

        int best = -1;
        float bestSqr = -1f;
        Vector2 pos = transform.position;
        for (int i = 0; i < _guardPoints.Count; i++)
        {
            if (i == old) continue;                      // 绝不选回当前点（否则换点=原地，死循环卡死）
            if (_claimedPoints.Contains(i)) continue;    // 避开同族已认领的点（防扎堆）
            float sqr = ((Vector2)_guardPoints[i] - pos).sqrMagnitude;
            if (sqr > bestSqr)
            {
                bestSqr = sqr;
                best = i;
            }
        }

        // 无空位（认领点被同族占满）→ 随机换一个非当前点，保证蜜蜂持续巡游不停滞
        if (best < 0)
        {
            int fallback = Random.Range(0, _guardPoints.Count - 1);
            if (fallback >= old) fallback++;
            best = fallback;
            if (_debugLog)
                Debug.Log($"[BeeAI][守护] {name} 认领点已满，随机换点 #{best}", this);
        }

        _guardIndex = best;
        FlockManager.ClaimPoint(_flock, _guardIndex);
        if (_debugLog)
            Debug.Log($"[BeeAI][守护] {name} 到达巡游点 → 换点 #{_guardIndex}（持续巡游）", this);
    }

    /// <summary>
    /// 挑选未被同族认领的巡游点（同族通信）：优先选无人认领的；全被占时随机兜底。
    /// 蜜蜂生成时各认领不同巡游点，天然错开位置，降低扎堆与轨道交叉。
    /// 兜底随机排除当前点，避免连续停在同一位置。
    /// </summary>
    private int PickUnclaimedPoint()
    {
        FlockManager.GetClaimedPoints(_flock, _claimedPoints);

        // 收集未认领的候选
        List<int> free = null;
        for (int i = 0; i < _guardPoints.Count; i++)
        {
            if (_claimedPoints.Contains(i)) continue;
            free ??= new List<int>();
            free.Add(i);
        }

        if (free != null && free.Count > 0)
            return free[Random.Range(0, free.Count)];

        // 全被认领（蜜蜂数 > 巡游点数）：随机兜底，但尽量避开当前点（防原地打转）
        if (_guardPoints.Count <= 1) return 0;
        int fallback = Random.Range(0, _guardPoints.Count - 1);
        if (fallback >= _guardIndex) fallback++;
        return fallback;
    }

    /// <summary>攻击目标位置（BeeAStarMoveAction.targetProvider）。无目标时返回自身（原地待命）。</summary>
    public Vector2 TargetPosition => _target != null ? _target.Position : (Vector2)transform.position;

    /// <summary>A* 额外代价：区域外高代价（限定活动范围）；无区域时 0（不限制）。</summary>
    public float CostAt(Vector2 worldPos)
    {
        if (_region == null) return 0f;
        return _region.Contains(worldPos) ? 0f : _region.OutsideCost;
    }

    /// <summary>A* 移动执行（move 委托）：按寻路方向飞行（含 Boids 三力修正）。</summary>
    public void MoveAlong(Vector2 direction, float speedMultiplier = 1f)
    {
        Fly(direction, speedMultiplier);
    }

    /// <summary>A* 到达目标后停止飞行。</summary>
    public void StopFly()
    {
        Hover();
    }

    /// <summary>贴身蜜刺：到达攻击目标身边后执行（BeeBT 攻击分支第二节点调用）。</summary>
    public void StingIfClose()
    {
        if (_target == null) { Hover(); return; }

        float sqr = ((Vector2)transform.position - _target.Position).sqrMagnitude;
        if (sqr <= _stingRange * _stingRange)
            TrySting();
        else
            Hover(); // 目标已离开：悬停，等待 A* 节点重算路径继续接近
    }

    /// <summary>蜜刺范围（BeeBT 攻击分支 A* 到达半径，与此一致）。</summary>
    public float StingRange => _stingRange;

    /// <summary>守护巡游到达半径（BeeBT 守护分支 A* 到达半径，与此一致）。</summary>
    public float GuardArriveRadius => _guardArriveRadius;

    /// <summary>飞散：背离目标飞出屏幕后销毁（被驱散的表现）。</summary>
    public void Scatter()
    {
        Vector2 away = Vector2.right;
        if (_target != null)
        {
            Vector2 fromTarget = (Vector2)transform.position - _target.Position;
            if (fromTarget.sqrMagnitude > 0.0001f) away = fromTarget.normalized;
        }

        Fly(away);

        // 出屏销毁
        if (_mainCamera != null)
        {
            Vector3 view = _mainCamera.WorldToViewportPoint(transform.position);
            if (view.x < -_scatterMargin || view.x > 1f + _scatterMargin ||
                view.y < -_scatterMargin || view.y > 1f + _scatterMargin)
                Destroy(gameObject);
        }
    }

    // ===================== 移动与伤害 =====================

    /// <summary>
    /// 生成守护巡游点：**泊松盘采样**（圆内随机散布 + 最小间距约束）——
    /// 在蜂巢周围圆盘内随机取点，点间距离 ≥ _minPointSeparation（不互相打架），
    /// 同时保证区域内/网格可通行。点铺满整个圆盘而非集中在某个局部，
    /// 蜜蜂依次巡游这些点 → 巡逻轨迹覆盖蜂巢四周一整圈。
    /// </summary>
    private void BuildGuardPoints()
    {
        _guardPoints.Clear();
        _guardIndex = 0;
        if (_hiveAnchor == null || _guardPointCount <= 0) return;

        float minY = _hiveAnchor.position.y + _guardMinHeightOffset;

        // 拒绝原因计数（诊断）：统计区域外/网格阻塞/间距不足各占多少，判断点生成不足的根因
        int rejRegion = 0, rejBlocked = 0, rejSpacing = 0, rejOutsideGrid = 0;

        // 泊松盘采样：为每个巡游点随机试位（圆内半径平方分布保证均匀），满足间距即接受
        const int maxAttempts = 60; // 每个点的最大随机尝试次数
        for (int i = 0; i < _guardPointCount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxAttempts && !placed; attempt++)
            {
                // 圆内均匀随机点：半径平方分布（√r）保证面积均匀，不是只集中在圆边/圆心
                float radius = _guardRadius * Mathf.Sqrt(Random.value);
                float angle = Random.Range(0f, 2f * Mathf.PI);
                Vector2 candidate = (Vector2)_hiveAnchor.position +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (candidate.y < minY) candidate.y = minY;

                if (_region != null && !_region.Contains(candidate)) { rejRegion++; continue; }
                if (!IsCellFree(candidate)) { if (cellOutsideGrid(candidate)) rejOutsideGrid++; else rejBlocked++; continue; }
                if (!IsFarFromPlaced(candidate)) { rejSpacing++; continue; } // 不互相打架（间距约束）

                _guardPoints.Add(candidate);
                placed = true;
            }
            // 尝试完仍失败：跳过此点（不追加，保持已有点的均匀骨架）
        }

        // 兜底：全失败时用蜂巢上方一点
        if (_guardPoints.Count == 0)
            _guardPoints.Add(new Vector2(_hiveAnchor.position.x, minY));

        // 点数不足（泊松盘在区域/间距约束下没生成满）→ 用等角均布圆周点补足到 _guardPointCount。
        // 保证"点数 ≥ 蜜蜂数"，认领机制才有足够点可分，不会全部蜜蜂挤同一个点。
        for (int i = _guardPoints.Count; i < _guardPointCount; i++)
        {
            float angle = 2f * Mathf.PI * i / _guardPointCount;
            Vector2 basePos = (Vector2)_hiveAnchor.position +
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _guardRadius;
            Vector2 fallback = basePos;
            if (fallback.y < minY) fallback.y = minY;
            _guardPoints.Add(fallback);
        }

        // 诊断：输出每个巡游点的坐标 + 拒绝原因计数（判断"点没铺满"是区域/网格/间距哪个导致的）
        string pts = "";
        for (int i = 0; i < _guardPoints.Count; i++)
        {
            Vector2 p = _guardPoints[i];
            pts += $" #{i}({p.x:F1},{p.y:F1})";
        }
        Debug.Log($"[BeeAI] {name} 巡游点{_guardPoints.Count}/{_guardPointCount}个（拒绝:区域{rejRegion}/网格{rejBlocked}+越界{rejOutsideGrid}/间距{rejSpacing}）{pts}", this);
    }

    /// <summary>间距约束（兜底）：候选点与所有已选巡游点的距离都 ≥ _minPointSeparation 才通过，防收缩后重叠。</summary>
    private bool IsFarFromPlaced(Vector2 candidate)
    {
        if (_minPointSeparation <= 0f) return true;
        for (int i = 0; i < _guardPoints.Count; i++)
        {
            if (Vector2.Distance(candidate, _guardPoints[i]) < _minPointSeparation)
                return false;
        }
        return true;
    }

    /// <summary>该点所在网格格是否可通行（蜜蜂寻路忽略悬空，只避物理障碍）。无网格视为可通行。</summary>
    private bool IsCellFree(Vector2 worldPos)
    {
        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null) return true;
        Vector2Int cell = grid.WorldToCell(worldPos);
        if (cell.x < 0) return false; // 网格外视为不可通行（巡游点不越出网格边界）
        return !grid.IsBlockedFor(cell.x, cell.y, true); // 蜜蜂忽略悬空格
    }

    /// <summary>诊断辅助：该世界坐标是否落在 NavGrid2D 网格边界外（用于区分"越界"与"网格内被障碍阻塞"）。</summary>
    private bool cellOutsideGrid(Vector2 worldPos)
    {
        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null) return false;
        return grid.WorldToCell(worldPos).x < 0;
    }

    /// <summary>按导航方向飞行（Boids 三力修正 + 随机噪声），无邻居时退化为纯导航。</summary>
    private void Fly(Vector2 baseDirection, float speedMultiplier = 1f)
    {
        Vector2 finalDir = baseDirection;
        if (_rb == null) return;

        int count = FlockManager.GetNeighbors(_flock, _neighborRadius, _neighbors);
        if (count > 0)
            finalDir = BoidsSteering.Apply(baseDirection, transform.position, _rb.velocity,
                _neighbors, _separationRadius, _separationWeight, _alignmentWeight, _cohesionWeight, _maxSteer);

        // 随机噪声：叠加微小随机扰动，让飞行更混乱自然（蜜蜂悬停采样/方向微摆）
        if (_wanderNoise > 0f)
            finalDir = (finalDir + Random.insideUnitCircle * _wanderNoise).normalized;

        _rb.velocity = finalDir * _flySpeed * speedMultiplier;
    }

    /// <summary>悬浮（无目标方向时的兜底）。</summary>
    private void Hover()
    {
        if (_rb != null) _rb.velocity = Vector2.zero;
    }

    /// <summary>蜜刺：冷却内对目标造成固定伤害。</summary>
    private void TrySting()
    {
        if (Time.time < _nextStingTime) return;
        if (!IsTargetAlive) return;

        _nextStingTime = Time.time + _stingCooldown;
        _target.TakeDamage(_stingDamage);
    }

#if UNITY_EDITOR
    /// <summary>Scene 可视化：画出守护巡游点（黄点）与当前目标点（红圈），便于排查扎堆/卡死。</summary>
    private void OnDrawGizmos()
    {
        if (_guardPoints == null || _guardPoints.Count == 0) return;

        // 巡游点（黄点）
        Gizmos.color = Color.yellow;
        foreach (Vector2 p in _guardPoints)
            Gizmos.DrawSphere(p, 0.12f);

        // 当前目标巡游点（红圈）
        if (_guardIndex < _guardPoints.Count)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_guardPoints[_guardIndex], _guardArriveRadius);
        }
    }
#endif
}
