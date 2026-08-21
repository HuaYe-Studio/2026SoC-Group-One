using UnityEngine;

/// <summary>
/// 蜂巢：环境物体 + BOSS 战的破坏目标。
/// - 蜜蜂常驻：开局在蜂巢周围生成蜜蜂（守护态，像鱼群一样是环境的一部分），
///   平时绕巢盘旋，蜂巢被破坏后由本组件通知蜜蜂切入攻击态。
/// - 破坏契约：BOSS 落点命中判定调用 TakeHit / 订阅 OnDestroyed；
///   破坏全部蜂巢 → BossController.NotifyHiveDestroyed → 进入胜利时序（PendingVictory）。
/// 蜜蜂与攻击目标均面向接口（IAttackTarget）编程，本组件不依赖具体类。
/// </summary>
public class Hive : MonoBehaviour
{
    [Header("蜂巢配置")]
    [SerializeField] private int hiveIndex = 0;

    [Tooltip("破坏所需命中次数")]
    [SerializeField] private int hitsToDestroy = 1;

    [Header("蜜蜂（常驻）")]
    [Tooltip("蜜蜂预制体（挂 BeeAI + BeeBT + FlockMember）。为空则不生成蜜蜂")]
    [SerializeField] private GameObject _beePrefab;
    [Tooltip("常驻蜜蜂数量（开局在蜂巢周围生成，守护盘旋；蜂巢破坏后切换攻击态）")]
    [SerializeField] private int _beeCount = 6;
    [Tooltip("蜜蜂出生点相对蜂巢的散布半径（米）")]
    [SerializeField] private float _beeSpawnRadius = 1f;
    [Tooltip("蜜蜂攻击的目标（挂 IAttackTarget 的单位，如 BossController）。为空时自动扫描场景中任意 IAttackTarget")]
    [SerializeField] private MonoBehaviour _targetComponent;

    [Header("蜜蜂活动区域（统一配置，生成时注入每只蜜蜂）")]
    [Tooltip("蜜蜂活动区域（AnimalRegion，推荐 Generic 类型，多边形圈定蜂巢范围）。\n区域内低代价、区域外高代价，蜜蜂 A* 寻路尽量不飞出区域。\n留空时自动查找场景中的 Generic 区域；仍无则不限制")]
    [SerializeField] private AnimalRegion _region;

    private int _currentHits;
    private bool _isDestroyed;
    private IAttackTarget _target;   // 运行时解析（不直接依赖具体类）
    private readonly System.Collections.Generic.List<BeeAI> _bees = new System.Collections.Generic.List<BeeAI>();

    /// <summary>蜂巢编号（1/2/3，供 OnHiveDestroyed 事件与 UI 定位）。</summary>
    public int HiveIndex => hiveIndex;

    /// <summary>是否已破坏。</summary>
    public bool IsDestroyed => _isDestroyed;

    /// <summary>剩余所需命中次数。</summary>
    public int RemainingHits => Mathf.Max(0, hitsToDestroy - _currentHits);

    /// <summary>蜂巢被破坏事件（→ BossController.NotifyHiveDestroyed）。</summary>
    public event System.Action<Hive> OnDestroyed;

    /// <summary>
    /// 受击（BOSS 落点命中判定调用）。hitPoint 在蜂巢判定盒内时累计次数，达阈值即破坏。
    /// 破坏只触发一次：触发 OnDestroyed 事件 + 广播 MockEventCenter.OnHiveDestroyed +
    /// 通知常驻蜜蜂切入攻击态（不再重复生成）。
    /// </summary>
    public void TakeHit(Vector2 hitPoint)
    {
        if (_isDestroyed) return;

        _currentHits++;
        Debug.Log($"[Hive] 蜂巢#{hiveIndex} 受击（{_currentHits}/{hitsToDestroy}）命中点=({hitPoint.x:F1},{hitPoint.y:F1})", this);
        if (_currentHits < hitsToDestroy) return;

        _isDestroyed = true;
        Debug.Log($"[Hive] 蜂巢#{hiveIndex} 被破坏！", this);
        OnDestroyed?.Invoke(this);
        MockEventCenter.TriggerHiveDestroyed(hiveIndex);
        NotifyBeesHiveDestroyed();
        // 表现（模型隐藏/粒子/音效）由表现层处理，这里仅禁用碰撞与渲染占位
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;
    }

    private void Start()
    {
        SpawnBees();
    }

    /// <summary>
    /// 常驻生成蜜蜂群：开局在蜂巢周围生成（守护态，绕巢巡游），与鱼群同套 Boids 结群。
    /// 同巢蜜蜂共享群 ID（BeeHive_编号），互为邻居成团行动。
    /// 目标通过 IAttackTarget 接口解析，不依赖具体类；解析不到则靠蜜蜂自检感知兜底。
    /// 活动区域由本组件统一配置，经 Init 注入每只蜜蜂（蜜蜂自身不逐只配置）。
    /// </summary>
    private void SpawnBees()
    {
        if (_beePrefab == null || _beeCount <= 0) return;

        if (_target == null)
            _target = ResolveTarget();
        if (_region == null)
            _region = ResolveRegion();

        for (int i = 0; i < _beeCount; i++)
        {
            Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * _beeSpawnRadius;
            GameObject beeGo = BeePool.Get(_beePrefab, spawnPos, Quaternion.identity);

            BeeAI bee = beeGo.GetComponent<BeeAI>();
            if (bee != null)
            {
                // 同巢蜜蜂同群：必须先设置 FlockMember 群 ID 再 Init——
                // Init 内部会用 FlockId 查询同族巡游点认领表做避让，若用默认 ID 会导致
                // 所有蜜蜂看不到彼此的认领，初始巡游点可能全撞同一个（扎堆）。
                FlockMember flock = beeGo.GetComponent<FlockMember>();
                if (flock != null)
                    flock.SetFlockId($"BeeHive_{hiveIndex}");

                bee.Init(transform, _target, _region);
                _bees.Add(bee);
            }
        }
    }

    /// <summary>蜂巢被破坏 → 通知常驻蜜蜂切入攻击态（倒序遍历防销毁）。</summary>
    private void NotifyBeesHiveDestroyed()
    {
        for (int i = _bees.Count - 1; i >= 0; i--)
        {
            if (_bees[i] == null)
            {
                _bees.RemoveAt(i);
                continue;
            }
            _bees[i].SetHiveDestroyed();
        }
    }

    /// <summary>
    /// 解析攻击目标：优先 Inspector 指定（挂 IAttackTarget 的组件）；
    /// 否则扫描场景中任意实现 IAttackTarget 的单位（通用兜底，不写死具体类）。
    /// </summary>
    private IAttackTarget ResolveTarget()
    {
        if (_targetComponent != null)
            return _targetComponent as IAttackTarget;

        var all = FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
            if (all[i] is IAttackTarget t)
                return t;
        return null;
    }

    /// <summary>
    /// 解析蜜蜂活动区域：优先 Inspector 指定；否则自动查找场景中的 Generic 区域（蜜蜂活动区）；
    /// 仍无则返回 null（不限制，只受 NavGrid2D 网格边界约束）。
    /// </summary>
    private AnimalRegion ResolveRegion()
    {
        if (_region != null)
            return _region;

        var regions = FindObjectsOfType<AnimalRegion>();
        for (int i = 0; i < regions.Length; i++)
            if (regions[i].Type == AnimalRegion.RegionType.Generic)
                return regions[i];
        return null;
    }

    /// <summary>重置蜂巢（BOSS 重开/重打时用）。</summary>
    public void ResetHive()
    {
        _currentHits = 0;
        _isDestroyed = false;
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = true;
    }
}
