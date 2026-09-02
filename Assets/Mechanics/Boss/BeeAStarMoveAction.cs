using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 蜜蜂 A* 寻路移动节点（蜜蜂专属，不依赖 AnimalBase）。
/// 采用鱼群同套寻路方案（NavGrid2D + AStarPathfinder），按需求只取 A* + 区域 + 障碍避开：
/// - ground/障碍不可通行格：由 NavGrid2D 烘焙的 blocked 数组判定（自动绕开地形）
/// - 区域内低代价 / 区域外高代价：costAt 委托注入（BeeAI 配置 AnimalRegion，蜂巢区域）
/// - 移动执行：move 委托注入（蜜蜂 Fly + Boids 三力修正，与鱼群同套群体算法）
/// 无上浮/下潜需求：蜜蜂全向飞行，A* 输出方向直接驱动 velocity。
/// </summary>
public class BeeAStarMoveAction : BTNode
{
    private readonly BeeAI _bee;
    private readonly System.Func<Vector2> _targetProvider;
    private readonly System.Func<Vector2, float> _costAt;
    private readonly float _speedMultiplier;
    private readonly float _arriveRadius;
    private readonly float _repathInterval;
    private readonly System.Action<Vector2, float> _move;
    private readonly System.Action _onArrive; // 到达目标后回调（守护分支用：到达巡游点后换下一个点）

    // 兜底网格：Instance 为 null 时懒查找场景中的 NavGrid2D 并缓存复用（场景若完全没放则无解）
    private static NavGrid2D _cachedGrid;

    private List<Vector2> _path;
    private int _pathIndex;
    private Vector2 _target;
    private float _nextRepathTime;

    /// <param name="targetProvider">目标点来源（每次 Tick 调用，可返回变化的目标）</param>
    /// <param name="costAt">额外代价函数（世界坐标→额外代价，如区域外高代价），可为 null</param>
    /// <param name="repathInterval">路径定时重算间隔（秒）</param>
    /// <param name="move">移动委托（方向向量, 速度倍率）</param>
    /// <param name="onArrive">到达目标后的回调（守护巡游用：到达后换下一个巡游点）。null = 不回调</param>
    public BeeAStarMoveAction(BeeAI bee,
        System.Func<Vector2> targetProvider,
        System.Func<Vector2, float> costAt = null,
        float speedMultiplier = 1f,
        float arriveRadius = 0.5f,
        float repathInterval = 0.5f,
        System.Action<Vector2, float> move = null,
        System.Action onArrive = null)
    {
        _bee = bee;
        _targetProvider = targetProvider;
        _costAt = costAt;
        _speedMultiplier = speedMultiplier;
        _arriveRadius = arriveRadius;
        _repathInterval = repathInterval;
        _move = move;
        _onArrive = onArrive;

        // 随机错峰：同批蜜蜂若都在同帧重算路径会瞬间集中 A*（掉帧），
        // 给每只蜜蜂一个 0~repathInterval 的初始偏移，把重算分摊到不同帧。
        _nextRepathTime = Time.time + Random.Range(0f, _repathInterval);
    }

    protected override State DoTick()
    {
        Vector2 position = (Vector2)_bee.transform.position;
        Vector2 desired = _targetProvider != null ? _targetProvider() : position;

        // 已到达目标点 → 触发换点回调 → 停飞并返回 Success
        // 注意：到达判定必须放在"目标变化重算"之前。若让 targetProvider 在到达时切换目标，
        // 同帧会"切换→立即判断到达新点→Success→再切换"死循环，蜜蜂永久悬停不动。
        if (Vector2.Distance(position, desired) <= _arriveRadius)
        {
            _onArrive?.Invoke();
            _path = null;
            _bee.StopFly();
            if (_bee.DebugLog)
                Debug.Log($"[BeeAI][寻路] {_bee.name} 到达目标 ({desired.x:F1},{desired.y:F1})", _bee);
            return State.Success;
        }

        // 无路径或目标变化 → 重算路径（定时刷新）
        if (_path == null || _path.Count == 0)
        {
            if (Time.time >= _nextRepathTime)
                Repath(position, desired);
        }
        else if (Vector2.Distance(desired, _target) > _arriveRadius * 2f || Time.time >= _nextRepathTime)
        {
            Repath(position, desired);
        }

        // 重算后仍无路径（寻路失败）→ 悬停原地等下个周期再试
        if (_path == null || _path.Count == 0)
        {
            _bee.StopFly();
            return State.Running;
        }

        // 推进到下一路径点
        while (_pathIndex < _path.Count &&
               Vector2.Distance(position, _path[_pathIndex]) <= _arriveRadius)
            _pathIndex++;

        // 到达终点
        if (_pathIndex >= _path.Count)
        {
            _path = null;
            _bee.StopFly();
            return State.Success;
        }

        MoveToward(position, _path[_pathIndex]);
        return State.Running;
    }

    public override void Reset()
    {
        _path = null;
        _pathIndex = 0;
        _target = Vector2.zero;
        _nextRepathTime = Time.time + Random.Range(0f, _repathInterval);
    }

    private void Repath(Vector2 position, Vector2 desired)
    {
        _target = desired;
        _nextRepathTime = Time.time + _repathInterval;

        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null)
        {
            // Instance 未设置（场景物体没在 Awake 前激活/或缺失）→ 懒查找一次并缓存
            if (_cachedGrid == null)
                _cachedGrid = Object.FindObjectOfType<NavGrid2D>();
            grid = _cachedGrid;
        }
        if (grid == null)
        {
            _path = null;
            if (_bee.DebugLog)
                Debug.Log($"[BeeAI][寻路] {_bee.name} 场景中无 NavGrid2D！请在场景添加挂 NavGrid2D 的物体（配 Obstacle Layers=Ground），蜜蜂才能寻路", _bee);
            return;
        }

        _path = new List<Vector2>();
        // 蜜蜂是飞行单位：忽略悬空格（_groundBlocked），只避物理障碍（ground/岩石/危险物）
        bool found = AStarPathfinder.FindPath(grid, position, desired, _costAt, _path, true);
        _pathIndex = 0;

        // 寻路失败：清空路径（走"无路径→悬停等重算"分支）。
        // 不检查返回值时，失败也会留下 1 个钳制终点，蜜蜂会直飞顶墙被卡住（0.5s 重算仍失败→永久停住）。
        if (!found)
        {
            _path.Clear();
            if (_bee.DebugLog)
                Debug.Log($"[BeeAI][寻路] {_bee.name} 寻路失败！pos=({position.x:F1},{position.y:F1}) target=({desired.x:F1},{desired.y:F1}) → 悬停等重算", _bee);
        }
        else if (_bee.DebugLog && _path.Count <= 1)
        {
            Debug.Log($"[BeeAI][寻路] {_bee.name} 寻路成功但路径过短（{_path.Count} 点），target=({desired.x:F1},{desired.y:F1})", _bee);
        }

        // Catmull-Rom 平滑：把 A* 折线转成平滑曲线，避免机械直线横移（折线感）
        if (_path.Count >= 2)
            _path = SmoothPath(position, _path);
    }

    /// <summary>
    /// Catmull-Rom 样条平滑：把 A* 折线路径转成平滑曲线。
    /// 曲线保证经过原始路径点（不偏移障碍），只在相邻段之间做弧度过渡。
    /// 采样密度按段长自适应（每段 4~6 点），密度适中不爆炸。
    /// </summary>
    private static List<Vector2> SmoothPath(Vector2 start, List<Vector2> path)
    {
        List<Vector2> smoothed = new List<Vector2>();
        int n = path.Count;

        // 第一段：起点→路径点0，直接用起点作为虚拟前驱保证首段也平滑
        Vector2 pPrev = start;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = i > 0 ? path[i - 1] : pPrev;          // 前驱（首段用起点）
            Vector2 p1 = path[i];                              // 当前段起点
            Vector2 p2 = path[i + 1];                          // 当前段终点
            Vector2 p3 = i + 2 < n ? path[i + 2] : p2;         // 后继（末段用自身）

            // 段长决定采样数：短段少采、长段多采（保持平滑但不密集）
            float segLen = Vector2.Distance(p1, p2);
            int samples = Mathf.Clamp(Mathf.CeilToInt(segLen / 0.35f), 3, 6);

            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)samples;
                smoothed.Add(SplineMath.CatmullRom(p0, p1, p2, p3, t));
            }
        }

        // 末尾补最终路径点（t=1 处，保证到达）
        smoothed.Add(path[n - 1]);
        return smoothed;
    }

    private void MoveToward(Vector2 position, Vector2 target)
    {
        Vector2 toTarget = target - position;
        float dist = toTarget.magnitude;
        if (dist < 0.0001f) return;

        Vector2 direction = toTarget / dist;
        if (_move != null)
            _move(direction, _speedMultiplier);
    }
}
