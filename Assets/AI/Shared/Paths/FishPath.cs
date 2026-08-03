using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用路径组件：在场景中用编辑器工具绘制路径点，运行时按 Catmull-Rom 样条平滑采样。
/// 只提供纯几何能力（采样点/段方向/最近位置），不包含任何动画或动物逻辑——
/// 「段走向 → 动画名」的映射由各动物的行为树负责，保持低耦合。
/// 用法：场景中创建空物体挂载本组件，然后在 Scene 视图中用场景工具绘制/拖动路径点。
/// </summary>
[DisallowMultipleComponent]
public class FishPath : MonoBehaviour
{
    /// <summary>路径用途：Normal=巡游，Escape=逃生（按威胁方向选择）。</summary>
    public enum PathType
    {
        Normal,
        Escape
    }

    [Header("Path")]
    [SerializeField] private PathType _pathType = PathType.Normal;

    [Tooltip("路径点（世界坐标），按顺序连线，运行时在点之间以样条平滑过渡")]
    [SerializeField] private List<Vector2> _points = new List<Vector2>();

    [Tooltip("到达终点后的行为：true=循环回到起点继续巡游，false=到达终点后停在原地")]
    [SerializeField] private bool _loop = true;

    [Header("Follow")]
    [Tooltip("路径跟随速度倍率（相对动物的移动速度）")]
    [SerializeField] private float _speedMultiplier = 1f;

    [Tooltip("判定「到达路径点」的距离半径（米）")]
    [SerializeField] private float _arriveRadius = 0.4f;

    public List<Vector2> Points => _points;
    public bool Loop => _loop;
    public float SpeedMultiplier => _speedMultiplier;
    public float ArriveRadius => _arriveRadius;
    public PathType Type => _pathType;

    /// <summary>
    /// 路径整体方向（起点→终点归一化向量）。
    /// 当路径作为"方向向量"使用时，它是路径的指引方向（不随锚点平移变化）。
    /// </summary>
    public Vector2 Direction
    {
        get
        {
            if (_points == null || _points.Count < 2)
                return Vector2.zero;
            return (_points[_points.Count - 1] - _points[0]).normalized;
        }
    }

    /// <summary>
    /// 以 anchor 为路径起点的世界采样点：把路径整体平移，使原起点对齐到 anchor。
    /// 用于逃生等场景——鱼在任意位置触发逃生时，路径从它脚下展开，方向保持不变。
    /// </summary>
    public Vector2 GetWorldPoint(int index, Vector2 anchor)
    {
        if (_points == null || _points.Count == 0)
            return anchor;
        return anchor + (_points[index] - _points[0]);
    }

    /// <summary>
    /// 以 anchor 为路径起点的样条采样点（globalT∈[0,1]）。
    /// </summary>
    public Vector2 SamplePoint(float globalT, Vector2 anchor)
    {
        Vector2 local = SamplePoint(globalT);
        return anchor + (local - _points[0]);
    }

    /// <summary>
    /// 以 anchor 为路径起点的第 segmentIndex 段上 t∈[0,1] 处的样条插值点。
    /// </summary>
    public Vector2 GetSegmentPoint(int segmentIndex, float t, Vector2 anchor)
    {
        Vector2 local = GetSegmentPoint(segmentIndex, t);
        return anchor + (local - _points[0]);
    }

    /// <summary>
    /// 路径段数（点数为 n 时共有 n-1 段）。点数不足 2 时返回 0。
    /// </summary>
    public int SegmentCount => Mathf.Max(0, _points.Count - 1);

    /// <summary>
    /// 折线总长（各段直线长度之和，作为样条弧长的近似）。
    /// </summary>
    public float TotalLength
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < SegmentCount; i++)
                sum += Vector2.Distance(_points[i], _points[i + 1]);
            return sum;
        }
    }

    /// <summary>
    /// 取某一段的走向向量（终点 - 起点）。索引越界返回 zero。
    /// </summary>
    public Vector2 GetSegmentDirection(int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= SegmentCount)
            return Vector2.zero;
        return _points[segmentIndex + 1] - _points[segmentIndex];
    }

    /// <summary>
    /// 取第 segmentIndex 段上 t∈[0,1] 处的样条插值点。
    /// 首段/末段用端点自身补全控制点，避免样条回绕。
    /// </summary>
    public Vector2 GetSegmentPoint(int segmentIndex, float t)
    {
        int count = _points.Count;
        Vector2 p0 = segmentIndex > 0 ? _points[segmentIndex - 1] : _points[0];
        Vector2 p1 = _points[segmentIndex];
        Vector2 p2 = _points[segmentIndex + 1];
        Vector2 p3 = segmentIndex < count - 2 ? _points[segmentIndex + 2] : _points[count - 1];
        return SplineMath.CatmullRom(p0, p1, p2, p3, t);
    }

    /// <summary>
    /// 按全局进度 t∈[0,1] 采样路径上的点（自动换算到段索引 + 段内进度）。
    /// </summary>
    public Vector2 SamplePoint(float globalT)
    {
        if (SegmentCount <= 0)
            return transform.position;

        float clamped = Mathf.Clamp01(globalT);
        float scaled = clamped * SegmentCount;
        int segmentIndex = Mathf.Min((int)scaled, SegmentCount - 1);
        float t = scaled - segmentIndex;
        return GetSegmentPoint(segmentIndex, t);
    }

    /// <summary>
    /// 给定全局进度，返回其所在的段索引（用于段走向/动画判定）。
    /// </summary>
    public int SegmentIndexAt(float globalT)
    {
        if (SegmentCount <= 0)
            return -1;
        int i = Mathf.FloorToInt(Mathf.Clamp01(globalT) * SegmentCount);
        return Mathf.Clamp(i, 0, SegmentCount - 1);
    }

    /// <summary>
    /// 找路径上离世界坐标最近处的全局进度 t∈[0,1]。
    /// 用于节点初始化时把动物"吸附"到路径最近处起步。
    /// </summary>
    public float NearestT(Vector2 worldPos)
    {
        if (SegmentCount <= 0)
            return 0f;

        float bestT = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i < SegmentCount; i++)
        {
            float dist = SplineMath.DistanceToSegment(worldPos, _points[i], _points[i + 1], out float localT);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestT = (i + localT) / SegmentCount;
            }
        }

        return bestT;
    }

    /// <summary>
    /// 逃生路线是否被障碍物阻挡。逃生路线 = 画好的路径按 anchor 平移的平行副本
    /// （anchor 即鱼所在位置，路线从鱼脚下起步，方向与画好的路径一致）。
    /// 只检测平移后的路线自身各段：路线起点就是鱼所在位置，无需再检测入口段。
    /// 供逃生选路时剔除被障碍物堵住的路线（威胁方向 + 障碍物双重判断）。
    /// obstacleMask 为 0 时表示不检测，恒返回 false。
    /// </summary>
    public bool IsBlocked(Vector2 anchor, LayerMask obstacleMask)
    {
        if (obstacleMask == 0 || SegmentCount < 1)
            return false;

        for (int i = 0; i < SegmentCount; i++)
        {
            if (IsSegmentBlocked(anchor + (_points[i] - _points[0]),
                                 anchor + (_points[i + 1] - _points[0]), obstacleMask))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 单段直线是否被障碍物阻挡（忽略 trigger）。
    /// </summary>
    private static bool IsSegmentBlocked(Vector2 a, Vector2 b, LayerMask obstacleMask)
    {
        Vector2 direction = b - a;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
            return false;

        RaycastHit2D hit = Physics2D.Linecast(a, b, obstacleMask);
        return hit.collider != null && !hit.collider.isTrigger;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (SegmentCount < 1)
            return;

        // 按走向着色绘制折线（与编辑器工具一致的视觉约定）
        for (int i = 0; i < SegmentCount; i++)
        {
            Vector2 dir = GetSegmentDirection(i);
            Gizmos.color = Mathf.Abs(dir.y) > Mathf.Abs(dir.x)
                ? (dir.y > 0f ? Color.green : Color.red)
                : Color.cyan;
            Gizmos.DrawLine(_points[i], _points[i + 1]);
        }

        foreach (Vector2 p in _points)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(p, 0.12f);
        }
    }
#endif
}
