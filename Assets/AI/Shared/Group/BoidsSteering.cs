using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [群体] Boids 三力（聚合/对齐/分离）纯计算：输入自身状态与同群邻居，输出修正后的移动方向。
/// 定位是"偏移修正"：三力叠加在 A*/漫游给出的导航方向上，只调方向、不改目标——
/// 导航仍由寻路层负责，避免三力与 A* 互相抢方向。
/// </summary>
public static class BoidsSteering
{
    /// <summary>
    /// 计算 Boids 修正方向：导航方向 + 三力偏移（限幅后混合），归一化返回。
    /// 无邻居时原样返回导航方向（零开销退化为纯寻路移动）。
    /// </summary>
    /// <param name="baseDirection">导航方向（A*/漫游给出，归一化）</param>
    /// <param name="selfPos">自身世界坐标</param>
    /// <param name="selfVel">自身当前速度（对齐力输入）</param>
    /// <param name="neighbors">同群邻居列表（由 FlockManager.GetNeighbors 提供）</param>
    /// <param name="separationRadius">分离半径：小于此距离的邻居产生排斥</param>
    /// <param name="separationWeight">分离力权重（防扎堆，通常三力中最高）</param>
    /// <param name="alignmentWeight">对齐力权重（朝邻居平均速度方向修正）</param>
    /// <param name="cohesionWeight">聚合权重（朝邻居质心修正；建议 ≤0.3，过强会群体收缩挤成一团）</param>
    /// <param name="maxSteer">修正强度上限（0~1：三力相对导航方向的最大混合比例，防三力盖过导航）</param>
    public static Vector2 Apply(Vector2 baseDirection, Vector2 selfPos, Vector2 selfVel,
        List<FlockMember> neighbors, float separationRadius,
        float separationWeight, float alignmentWeight, float cohesionWeight, float maxSteer)
    {
        if (neighbors == null || neighbors.Count == 0)
            return baseDirection;

        Vector2 centroidSum = Vector2.zero;
        Vector2 velocitySum = Vector2.zero;
        Vector2 separation = Vector2.zero;
        int count = 0;

        foreach (FlockMember n in neighbors)
        {
            if (n == null) continue;

            Vector2 offset = (Vector2)n.transform.position - selfPos;
            centroidSum += (Vector2)n.transform.position;
            velocitySum += n.Velocity;

            // 分离：距离越近排斥越强（反比平方），限幅在下方统一处理
            float dist = offset.magnitude;
            if (dist < separationRadius && dist > 0.001f)
                separation -= offset / (dist * dist);

            count++;
        }

        if (count == 0)
            return baseDirection;

        Vector2 steer = Vector2.zero;

        // 聚合：朝邻居质心
        Vector2 toCentroid = centroidSum / count - selfPos;
        if (toCentroid.sqrMagnitude > 0.0001f)
            steer += cohesionWeight * toCentroid.normalized;

        // 对齐：朝邻居平均速度方向（群体整体静止时跳过）
        if (velocitySum.sqrMagnitude > 0.001f)
            steer += alignmentWeight * velocitySum.normalized;

        // 分离：贴脸时反比平方会爆炸，先限幅到单位长度再加权
        if (separation.sqrMagnitude > 1f)
            separation.Normalize();
        steer += separationWeight * separation;

        // 修正限幅：三力只调方向，不盖过导航
        if (steer.magnitude > maxSteer)
            steer = steer.normalized * maxSteer;

        Vector2 result = baseDirection + steer;
        return result.sqrMagnitude > 0.0001f ? result.normalized : baseDirection;
    }

    /// <summary>
    /// 地面动物（羊/蜘蛛等水平移动）的便捷版：把标量水平方向（±1 或连续值）包成二维向量
    /// 走三力修正，返回修正后的水平分量。漫游仅水平移动，垂直分量丢弃。
    /// </summary>
    /// <param name="direction">导航水平方向（-1~1，连续）</param>
    /// <returns>修正后的水平方向（-1~1，连续）</returns>
    public static float ApplyHorizontal(float direction, Vector2 selfPos, Vector2 selfVel,
        List<FlockMember> neighbors, float separationRadius,
        float separationWeight, float alignmentWeight, float cohesionWeight, float maxSteer)
    {
        Vector2 result = Apply(new Vector2(direction, 0f), selfPos, selfVel, neighbors,
            separationRadius, separationWeight, alignmentWeight, cohesionWeight, maxSteer);
        return result.x;
    }
}
