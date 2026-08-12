using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [群体] 群体注册表（静态）：维护 群ID → 成员列表，提供邻居/质心/平均朝向查询。
/// 成员由 FlockMember.OnEnable/OnDisable 自动注册与注销，无需场景单例。
/// 个体规模小（单群 &lt;20），邻居查询 O(n) 现算无缓存压力；
/// 遍历时顺带清理已销毁成员（fake null），兜底异常未注销的情况。
/// </summary>
public static class FlockManager
{
    private static readonly Dictionary<string, List<FlockMember>> _flocks =
        new Dictionary<string, List<FlockMember>>();

    /// <summary>注册成员（由 FlockMember.OnEnable 调用，重复注册自动去重）。</summary>
    public static void Register(FlockMember member)
    {
        if (member == null) return;

        string id = member.FlockId;
        if (!_flocks.TryGetValue(id, out List<FlockMember> members))
        {
            members = new List<FlockMember>();
            _flocks[id] = members;
        }

        if (!members.Contains(member))
            members.Add(member);
    }

    /// <summary>注销成员（由 FlockMember.OnDisable 调用）。</summary>
    public static void Unregister(FlockMember member)
    {
        if (member == null) return;

        if (_flocks.TryGetValue(member.FlockId, out List<FlockMember> members))
        {
            members.Remove(member);
            if (members.Count == 0)
                _flocks.Remove(member.FlockId);
        }
    }

    /// <summary>
    /// 收集同群邻居（不含自身，按距离过滤），结果写入 results（非分配，复用调用方缓冲）。
    /// 返回邻居数量（= results.Count）。
    /// </summary>
    public static int GetNeighbors(FlockMember self, float radius, List<FlockMember> results)
    {
        results.Clear();
        if (self == null) return 0;
        if (!_flocks.TryGetValue(self.FlockId, out List<FlockMember> members)) return 0;

        Vector2 selfPos = self.transform.position;
        float sqrRadius = radius * radius;

        // 倒序遍历：顺带清理已销毁成员（fake null）
        for (int i = members.Count - 1; i >= 0; i--)
        {
            FlockMember m = members[i];
            if (m == null)
            {
                members.RemoveAt(i);
                continue;
            }
            if (m == self) continue;

            if (((Vector2)m.transform.position - selfPos).sqrMagnitude <= sqrRadius)
                results.Add(m);
        }

        return results.Count;
    }

    /// <summary>群体质心（世界坐标）。群为空时返回 false。</summary>
    public static bool TryGetCentroid(string flockId, out Vector2 centroid)
    {
        centroid = Vector2.zero;
        if (!_flocks.TryGetValue(flockId, out List<FlockMember> members)) return false;

        int count = 0;
        for (int i = members.Count - 1; i >= 0; i--)
        {
            FlockMember m = members[i];
            if (m == null)
            {
                members.RemoveAt(i);
                continue;
            }
            centroid += (Vector2)m.transform.position;
            count++;
        }

        if (count == 0) return false;
        centroid /= count;
        return true;
    }

    /// <summary>群体平均朝向（成员速度归一化均值）。群为空或整体静止时返回 false。</summary>
    public static bool TryGetAverageHeading(string flockId, out Vector2 heading)
    {
        heading = Vector2.zero;
        if (!_flocks.TryGetValue(flockId, out List<FlockMember> members)) return false;

        int count = 0;
        for (int i = members.Count - 1; i >= 0; i--)
        {
            FlockMember m = members[i];
            if (m == null)
            {
                members.RemoveAt(i);
                continue;
            }
            heading += m.Velocity;
            count++;
        }

        if (count == 0 || heading.sqrMagnitude < 0.0001f) return false;
        heading.Normalize();
        return true;
    }
}
