using UnityEngine;

/// <summary>
/// [BT] 通用回撤节点：脱离危险后回到逃生起点（Blackboard.RetreatTarget）。
/// 直线可达（无碰撞体阻挡）时直线返回；被阻挡时拆成「水平段 + 垂直段」的折线，
/// 每段视为一条直线路径逐步推进。
/// 各阶段按当前移动方向播动画（上浮/下沉/前行），动画解析通过委托注入。
/// 到达目标点后清除回撤标记并返回 Success。
/// </summary>
public class BTReturnToPointAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly Blackboard _bb;
    private readonly float _speedMultiplier;
    private readonly float _arriveRadius;
    private readonly System.Action<Vector2, float> _move;
    private readonly System.Func<Vector2, string> _animResolver;

    // 回撤路径：直线或折线分解出的有序途经点（含终点）
    private Vector2[] _waypoints;
    private int _waypointIndex;

    // 折线检测用的碰撞层（返回途中的地形/障碍物）
    [System.NonSerialized] private int _obstacleLayer;

    /// <param name="speedMultiplier">回撤速度倍率</param>
    /// <param name="obstacleLayers">阻挡折线的碰撞层（地形等）；默认按移动委托自行判定</param>
    public BTReturnToPointAction(AnimalBase animal,
        float speedMultiplier = 1f, float arriveRadius = 0.4f,
        System.Action<Vector2, float> move = null,
        System.Func<Vector2, string> animResolver = null,
        int obstacleLayers = 0)
    {
        _animal = animal;
        _bb = animal.Board;
        _speedMultiplier = speedMultiplier;
        _arriveRadius = arriveRadius;
        _move = move;
        _animResolver = animResolver;
        _obstacleLayer = obstacleLayers;
    }

    public override State Tick()
    {
        if (!_bb.HasRetreatTarget)
        {
            Reset();
            return State.Success;
        }

        Vector2 destination = _bb.RetreatTarget;
        Vector2 position = (Vector2)_animal.transform.position;

        // 尚未开始或目标发生变化 → 规划回撤路径
        if (_waypoints == null)
            _waypoints = PlanReturnPath(position, destination);

        if (_waypointIndex >= _waypoints.Length)
        {
            // 已走完所有途经点 → 到达，清除回撤标记
            _bb.HasRetreatTarget = false;
            Reset();
            return State.Success;
        }

        Vector2 target = _waypoints[_waypointIndex];
        Vector2 toTarget = target - position;
        float distance = toTarget.magnitude;

        if (distance <= _arriveRadius)
        {
            _waypointIndex++; // 到达当前途经点，推进到下一段
            return State.Running;
        }

        // 朝当前途经点移动，并按方向播放动画
        Vector2 direction = toTarget / distance;
        if (_move != null)
            _move(direction, _speedMultiplier);
        else
            _animal.PerformMove(direction.x, _speedMultiplier);

        if (_animResolver != null)
        {
            string anim = _animResolver(direction);
            _animal.PlayAnimation(anim);
        }

        return State.Running;
    }

    public override void Reset()
    {
        _waypoints = null;
        _waypointIndex = 0;
    }

    /// <summary>
    /// 规划回撤路径：先尝试直线；若被碰撞体阻挡，则拆成先水平后垂直的折线。
    /// 折线两段都不可达时退回纯直线（尽力而为）。
    /// </summary>
    private Vector2[] PlanReturnPath(Vector2 from, Vector2 to)
    {
        if (IsLineClear(from, to))
            return new[] { to };

        // 折线：先水平到 (to.x, from.y)，再垂直到 to
        Vector2 midH = new Vector2(to.x, from.y);
        if (IsLineClear(from, midH) && IsLineClear(midH, to))
            return new[] { midH, to };

        // 折线：先垂直到 (from.x, to.y)，再水平到 to
        Vector2 midV = new Vector2(from.x, to.y);
        if (IsLineClear(from, midV) && IsLineClear(midV, to))
            return new[] { midV, to };

        // 均不可达：退回纯直线，物理上沿路滑动
        return new[] { to };
    }

    /// <summary>
    /// 直线是否被碰撞体阻挡（Physics2D.Raycast，全层）。
    /// 忽略触发器（鱼/玩家的 trigger 不应挡住返回路线）。
    /// </summary>
    private bool IsLineClear(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
            return true;

        int mask = _obstacleLayer != 0 ? _obstacleLayer : Physics2D.AllLayers;
        RaycastHit2D hit = Physics2D.Raycast(from, direction / distance, distance, mask);
        return hit.collider == null || hit.collider.isTrigger;
    }
}
