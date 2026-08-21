using UnityEngine;

/// <summary>
/// [群体] 恐惧传播 + 领头机制：挂载到群居动物（泡泡鱼等，羊/蜘蛛可复用）。
///
/// 恐惧传播（同类 + 次数上限 + 衰减）：
/// - 上升沿触发：自身威胁值跨过 _spreadThreshold 的瞬间，向半径内同类传播一次恐惧；
///   直接感知威胁（看到玩家）与接收传播抬升威胁 都算"跨线"，都能触发传播。
/// - 跳数上限：每次传播携带剩余跳数，接收者续传时跳数 -1，到 0 不再续传；
///   配合传播冷却（_spreadCooldown），杜绝群体内无限级联（次数上限）。
/// - 被攻击（吞噬/受击）时立即向群体传播一次满跳恐惧（_spreadOnAttacked）。
/// - 衰减：传播只抬升 ThreatLevel，回落仍由感知层（EnvironmentMonitor）的衰减速率负责，
///   本组件不做二次衰减，避免与感知层重复逻辑。
/// - 接收者会被写入短时威胁记忆（指向传播源对玩家的认知位置），
///   从而进入行为树"警觉/搜索"分支——鱼群朝出事地点查看。
///
/// 领头机制：
/// - 群内（同类相邻簇）威胁值最高者判定为领头（IsLeader），其余为跟随者（HasLeader 为真）。
/// - 决策层（如 BubbleFishBT 逃生分支）读取 IsLeader/Leader：跟随者在逃跑时朝领头游动，
///   实现"领头决定逃向、群体跟随逃窜"。
///
/// 同类识别：_speciesTag 按 Tag 区分同类（本项目动物共用 layer 7 Animal，层位不足以区分物种）。
/// 跨物种传播（IFearReceiver 口子）：_crossSpeciesTags 列表中的动物（挂 IFearReceiver）
/// 也会收到恐惧——但跳数恒为 0（不续传）、恐惧量按 _crossSpeciesFactor 衰减，
/// 例如鱼群受惊惊动岸边蜘蛛，恐慌不会跨物种级联放大。
/// </summary>
[RequireComponent(typeof(AnimalBase))]
public class FearSpreader : MonoBehaviour, IFearReceiver
{
    [Header("恐惧传播")]
    [Tooltip("传播半径（米）")]
    [SerializeField] private float _spreadRadius = 6f;

    [Tooltip("同类层位：物理扫描用的层（本项目动物层 = 7 Animal）。0 表示未配置，传播禁用")]
    [SerializeField] private LayerMask _fellowLayers;

    [Tooltip("同类 Tag：与自身同 Tag 的动物视为同类，可接收传播。空字符串表示未配置，传播禁用")]
    [SerializeField] private string _speciesTag = "";

    [Tooltip("传播触发阈值：自身威胁值跨过此值的瞬间触发一次传播")]
    [SerializeField] private float _spreadThreshold = 50f;

    [Tooltip("接收者的威胁抬升量（0~100 威胁值）")]
    [SerializeField] private float _fearAmount = 30f;

    [Tooltip("传播抬升的威胁上限：防止链式传播把整群顶到满值恐慌")]
    [SerializeField] private float _maxFearLevel = 80f;

    [Tooltip("传播冷却（秒）：同一动物两次传播的最小间隔")]
    [SerializeField] private float _spreadCooldown = 1.5f;

    [Tooltip("传播最大跳数（次数上限）：A→B→C 链深，到 0 不再续传")]
    [SerializeField] private int _maxHops = 2;

    [Tooltip("被攻击（吞噬/受击）时立即向群体传播一次恐惧")]
    [SerializeField] private bool _spreadOnAttacked = true;

    [Header("跨物种传播（口子）")]
    [Tooltip("跨物种接收 Tag 列表：传播半径内这些 Tag 的动物（挂 IFearReceiver）也会收到恐惧，但不可续传。空数组 = 仅同类传播")]
    [SerializeField] private string[] _crossSpeciesTags = new string[0];

    [Tooltip("跨物种恐惧衰减系数：跨物种接收的恐惧量 = 正常恐惧量 × 此系数（恐慌跨物种打折）")]
    [SerializeField, Range(0f, 1f)] private float _crossSpeciesFactor = 0.5f;

    [Header("领头机制")]
    [Tooltip("领头判定采样间隔（秒）")]
    [SerializeField] private float _leaderCheckInterval = 0.5f;

    private Blackboard _bb;
    private bool _wasAboveThreshold;   // 上一帧威胁值是否已过阈值（上升沿检测）
    private int _pendingHops;          // 接收恐惧后的剩余可传播跳数（0 = 无传播权）
    private float _nextSpreadTime;     // 下次传播允许时间（冷却）

    private Transform _leader;         // 群内威胁最高者
    private bool _isLeader;            // 自己是否就是领头
    private float _nextLeaderCheckTime;

    private static readonly Collider2D[] _hitBuffer = new Collider2D[16];

    /// <summary>自己是否为群内威胁最高者（领头）。</summary>
    public bool IsLeader => _isLeader;

    /// <summary>当前群内领头（威胁最高者）的 Transform；无则 null。</summary>
    public Transform Leader => _leader;

    /// <summary>是否存在可跟随的领头（非自身）。</summary>
    public bool HasLeader => _leader != null && _leader != transform;

    private void Awake()
    {
        AnimalBase animal = GetComponent<AnimalBase>();
        _bb = animal != null ? animal.Board : null;
        if (_bb != null)
            _wasAboveThreshold = _bb.ThreatLevel > _spreadThreshold;

        MockEventCenter.OnAnimalAttacked += OnAnimalAttacked;
    }

    private void OnDestroy()
    {
        MockEventCenter.OnAnimalAttacked -= OnAnimalAttacked;
    }

    private void OnAnimalAttacked(GameObject victim, GameObject attacker, float damage)
    {
        if (!_spreadOnAttacked || _bb == null) return;
        if (victim != gameObject) return;
        // 直接受击：立即传播一次满跳恐惧，同伴应声而散
        SpreadFear(_maxHops);
    }

    private void Update()
    {
        if (_bb == null) return;

        UpdateLeader();
        UpdateFearPropagation();
    }

    /// <summary>
    /// 领头判定：同类簇内威胁值最高者为领头（威胁相同则先到者胜）。
    /// 所有人威胁为 0 时自己也"是领头"，但决策层只在紧迫威胁时消费该标记，无副作用。
    /// </summary>
    private void UpdateLeader()
    {
        if (Time.time < _nextLeaderCheckTime) return;
        _nextLeaderCheckTime = Time.time + _leaderCheckInterval;

        Transform best = null;
        float bestThreat = _bb.ThreatLevel;

        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, _spreadRadius, _hitBuffer, _fellowLayers);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _hitBuffer[i];
            if (hit.transform == transform) continue;
            if (_speciesTag.Length > 0 && !hit.CompareTag(_speciesTag)) continue;

            AnimalBase other = hit.GetComponent<AnimalBase>();
            if (other == null) continue;

            float t = other.Board.ThreatLevel;
            if (t > bestThreat)
            {
                bestThreat = t;
                best = other.transform;
            }
        }

        _leader = best;
        _isLeader = best == null; // 簇内无人威胁更高 → 自己是领头
    }

    /// <summary>
    /// 上升沿传播：威胁值跨过阈值的瞬间传播一次。
    /// 直接感知威胁走满跳数（新源头）；接收传播抬升威胁的走剩余跳数（次数上限）。
    /// </summary>
    private void UpdateFearPropagation()
    {
        bool nowAbove = _bb.ThreatLevel > _spreadThreshold;
        bool crossed = nowAbove && !_wasAboveThreshold;
        _wasAboveThreshold = nowAbove;

        if (!crossed || Time.time < _nextSpreadTime) return;
        if (_fellowLayers.value == 0 || _speciesTag.Length == 0) return;

        int hops = _pendingHops > 0 ? _pendingHops : _maxHops;
        SpreadFear(hops);
    }

    /// <summary>
    /// 向半径内同类传播一次恐惧。
    /// 传播携带剩余跳数：接收者续传时跳数 -1，到 0 不再续传（次数上限）。
    /// 同类 = 同 Tag，走 IFearReceiver 接口派发；
    /// 跨物种（_crossSpeciesTags 列表）也接收，但跳数恒为 0（不续传）且恐惧量按系数衰减。
    /// </summary>
    private void SpreadFear(int hops)
    {
        _nextSpreadTime = Time.time + _spreadCooldown;
        _pendingHops = 0;

        // 传播源对玩家的认知位置（用于给接收者注入威胁记忆）
        bool sourceKnowsPlayer = _bb.IsPlayerVisible || _bb.LastSeenPlayerTime != float.NegativeInfinity;
        Vector2 knownPlayer = _bb.IsPlayerVisible
            ? _bb.AnimalPosition + _bb.PlayerDirection * _bb.PlayerDistance
            : _bb.LastKnownPlayerPos;

        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, _spreadRadius, _hitBuffer, _fellowLayers);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _hitBuffer[i];
            if (hit.transform == transform) continue;

            bool isFellow = _speciesTag.Length > 0 && hit.CompareTag(_speciesTag);
            if (!isFellow && !IsCrossSpeciesReceiver(hit)) continue;

            IFearReceiver receiver = hit.GetComponent<IFearReceiver>();
            if (receiver == null) continue;

            if (isFellow)
            {
                // 同类：正常传播，可续传（跳数 -1）
                receiver.ReceiveFear(knownPlayer, sourceKnowsPlayer, _fearAmount, hops - 1);
            }
            else
            {
                // 跨物种口子：只接收、不续传（hops 恒 0），恐惧量按系数衰减
                receiver.ReceiveFear(knownPlayer, sourceKnowsPlayer, _fearAmount * _crossSpeciesFactor, 0);
            }
        }
    }

    /// <summary>命中物是否配置的跨物种接收者（Tag 匹配 _crossSpeciesTags）。</summary>
    private bool IsCrossSpeciesReceiver(Collider2D hit)
    {
        for (int i = 0; i < _crossSpeciesTags.Length; i++)
        {
            if (hit.CompareTag(_crossSpeciesTags[i]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 接收同伴传播的恐惧（由其他 FearSpreader 调用）。
    /// - 抬升威胁（钳制 _maxFearLevel，防链式爆满）
    /// - 记录剩余跳数：>0 才允许自身跨线后续传（次数上限）
    /// - 源确知玩家位置时写入短时威胁记忆，使接收者进入"警觉/搜索"；
    ///   否则只抬升威胁，靠感知层自然衰减回稳。
    /// </summary>
    public void ReceiveFear(Vector2 sourceKnownPlayerPos, bool sourceKnowsPlayer, float amount, int hops)
    {
        if (_bb == null) return;

        _bb.ThreatLevel = Mathf.Min(_bb.ThreatLevel + amount, _maxFearLevel);

        if (hops > 0)
            _pendingHops = Mathf.Max(_pendingHops, hops);

        if (sourceKnowsPlayer)
            _bb.RememberThreat(sourceKnownPlayerPos, Time.time);
    }
}
