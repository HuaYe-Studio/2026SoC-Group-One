using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] A* 寻路移动节点：通过 NavGrid2D 寻路，沿路径点逐点移动到目标点。
/// 通用节点，替代原 BTPathFollowAction/BTPathEscapeAction/BTReturnToPointAction：
/// - 目标点来源由 targetProvider 委托提供（安全点/食物点/逃生点），支持运行时变化
/// - 额外代价（区域外空气高代价、玩家高代价）由 costAt 委托注入，网格保持通用
/// - 路径定时重算 + 目标变化立即重算（玩家高代价导致路径过期）
/// - 移动与动画通过委托注入（沿用现有节点风格，鱼走 Swim、陆地走 PerformMove）
/// </summary>
public class BTAStarMoveAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly Blackboard _bb;
    private readonly System.Func<Vector2> _targetProvider;
    private readonly System.Func<Vector2, float> _costAt;
    private readonly float _speedMultiplier;
    private readonly float _arriveRadius;
    private readonly float _repathInterval;
    private readonly System.Action<Vector2, float> _move;
    private readonly System.Func<Vector2, string> _animResolver;

    private List<Vector2> _path;
    private int _pathIndex;
    private Vector2 _target;
    private float _nextRepathTime;
    private string _lastAnim;

    /// <param name="targetProvider">目标点来源（每次 Tick 调用，可返回变化的目标）</param>
    /// <param name="costAt">额外代价函数（世界坐标→额外代价，如空气/玩家惩罚），可为 null</param>
    /// <param name="repathInterval">路径定时重算间隔（秒），玩家移动导致路径过期时的刷新频率</param>
    /// <param name="move">移动委托（方向向量, 速度倍率）；null 时用陆地 PerformMove</param>
    /// <param name="animResolver">动画解析（方向向量→动画名）；null 时不播动画</param>
    public BTAStarMoveAction(AnimalBase animal,
        System.Func<Vector2> targetProvider,
        System.Func<Vector2, float> costAt = null,
        float speedMultiplier = 1f,
        float arriveRadius = 0.5f,
        float repathInterval = 0.8f,
        System.Action<Vector2, float> move = null,
        System.Func<Vector2, string> animResolver = null)
    {
        _animal = animal;
        _bb = animal.Board;
        _targetProvider = targetProvider;
        _costAt = costAt;
        _speedMultiplier = speedMultiplier;
        _arriveRadius = arriveRadius;
        _repathInterval = repathInterval;
        _move = move;
        _animResolver = animResolver;
    }

    protected override State DoTick()
    {
        Vector2 position = (Vector2)_animal.transform.position;
        Vector2 desired = _targetProvider != null ? _targetProvider() : position;

        // 目标变化超过阈值 → 立即重算路径
        if (_path != null && Vector2.Distance(desired, _target) > _arriveRadius * 2f)
            Repath(position, desired);

        // 定时重算（玩家高代价导致路径过期）
        if (Time.time >= _nextRepathTime)
            Repath(position, desired);

        // 已到达目标点（含停留期：目标==当前位置时保持静止，不重复移动）
        if (Vector2.Distance(position, desired) <= _arriveRadius)
        {
            _path = null;
            _animal.StopMoving();
            return State.Success;
        }

        // 无路径（全阻塞等）→ 直线移动兜底
        if (_path == null || _path.Count == 0)
        {
            MoveToward(position, desired);
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
            _animal.StopMoving();
            return State.Success;
        }

        MoveToward(position, _path[_pathIndex]);
        return State.Running;
    }

    public override void Reset()
    {
        _path = null;
        _pathIndex = 0;
        _nextRepathTime = 0f;
        _lastAnim = null;
        _target = Vector2.zero;
    }

    private void Repath(Vector2 position, Vector2 desired)
    {
        _target = desired;
        _nextRepathTime = Time.time + _repathInterval;

        NavGrid2D grid = NavGrid2D.Instance;
        if (grid == null)
        {
            _path = null;
            return;
        }

        _path = new List<Vector2>();
        AStarPathfinder.FindPath(grid, position, desired, _costAt, _path);
        _pathIndex = 0;
    }

    private void MoveToward(Vector2 position, Vector2 target)
    {
        Vector2 toTarget = target - position;
        float dist = toTarget.magnitude;
        if (dist < 0.0001f) return;

        Vector2 direction = toTarget / dist;
        if (_move != null)
            _move(direction, _speedMultiplier);
        else
            _animal.PerformMove(direction.x, _speedMultiplier);

        if (_animResolver != null)
        {
            string anim = _animResolver(direction);
            if (anim != _lastAnim)
            {
                _lastAnim = anim;
                _animal.PlayAnimation(anim);
            }
        }
    }
}
