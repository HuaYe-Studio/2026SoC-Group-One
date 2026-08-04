using UnityEngine;

/// <summary>
/// 样条几何工具：Catmull-Rom 曲线插值等纯几何计算。
/// 不依赖任何游戏对象/动画，供路径组件与节点复用，保持低耦合。
/// </summary>
public static class SplineMath
{
    /// <summary>
    /// Catmull-Rom 样条插值：给定四个控制点，返回 p1→p2 段上 t∈[0,1] 处的曲线点。
    /// 曲线经过 p1、p2（p0/p3 提供切线方向）。
    /// </summary>
    public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// 点到线段的最短距离，并输出投影参数 t（0~1，表示落在线段上的比例）。
    /// </summary>
    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f)
        {
            t = 0f;
            return Vector2.Distance(p, a);
        }

        t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
