using UnityEngine;

/// <summary>
/// [BT] 通用路径跟随节点：Pure Pursuit 循迹算法，驱动动物沿 FishPath 样条路径平滑游动。
/// 优化点（相对旧版"最近路径点直游"）：
/// - 每帧用 NearestT 把进度吸附到路径最近处，天然校正横向偏差（离线路径也能拉回）；
/// - 目标点取"当前位置沿路径前方 lookahead 距离处"的前瞻点，轨迹平滑、不贴边滑行；
/// - 路径循环时前瞻点跨过末端自动回卷到起点，循环无停顿。
/// 通过委托解耦：
/// - move 委托：如何移动（如鱼用 Swim，陆地动物用 PerformMove）
/// - animResolver 委托：给定路径段走向，返回动画状态名（如 SwimUp/SwimForward）
/// 不传入委托时使用默认实现：水平移动 + 按走向判定 SwimUp/SwimDown/SwimForward。
/// 到达终点后按 FishPath.Loop 决定循环或停止。持续 Running。
/// </summary>
public class BTPathFollowAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly FishPath _path;

    private readonly System.Action<Vector2, float> _move;
    private readonly System.Func<Vector2, string> _animResolver;
    private readonly float _lookAheadOverride;

    private float _progress;   // 沿路径的全局进度 t∈[0,1]（最近点吸附）
    private bool _initialized;
    private bool _finished;    // 已到达终点（非循环路径）
    private string _lastAnim;

    private const int LookAheadSampleSteps = 8; // 弧长累计的采样步数/段
    private const float LookAheadTime = 0.6f;   // 前瞻点对应"前进时长"（秒），速度×此值=前瞻距离

    /// <param name="move">移动委托(direction, speedMultiplier)，null 时回退为水平 PerformMove</param>
    /// <param name="animResolver">动画解析委托(segmentDirection)->动画名，null 时按走向判定 SwimUp/SwimDown/SwimForward</param>
    /// <param name="lookAhead">前瞻距离（米），&lt;=0 时按移动速度自动估算</param>
    public BTPathFollowAction(AnimalBase animal, FishPath path,
        System.Action<Vector2, float> move = null,
        System.Func<Vector2, string> animResolver = null,
        float lookAhead = 0f)
    {
        _animal = animal;
        _path = path;
        _move = move;
        _animResolver = animResolver;
        _lookAheadOverride = lookAhead;
    }

    public override State Tick()
    {
        if (_path == null || _path.Points == null || _path.Points.Count < 2)
            return State.Failure;

        if (!_initialized)
            Initialize();

        // 已到终点且不循环：停在原地，保持 Running 等上层切换
        if (_finished)
        {
            _animal.StopMoving();
            return State.Running;
        }

        Vector2 position = (Vector2)_animal.transform.position;

        // Pure Pursuit：先把进度吸附到路径最近处（横向误差校正）
        _progress = _path.NearestT(position);

        // 目标 = 沿路径向前 lookahead 距离处的前瞻点
        float speed = GetMovementSpeed();
        float lookAhead = _lookAheadOverride > 0f ? _lookAheadOverride : Mathf.Max(_path.ArriveRadius, speed * LookAheadTime);
        Vector2 target = LookAheadPoint(_progress, lookAhead);

        Vector2 toTarget = target - position;

        // 接近路径末端（非循环）→ 到达终点
        if (!_path.Loop && _progress >= 0.999f && toTarget.magnitude <= _path.ArriveRadius)
        {
            _finished = true;
            _animal.StopMoving();
            return State.Running;
        }

        // 游向前瞻点
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Vector2 direction = toTarget.normalized;
            if (_move != null)
                _move(direction, _path.SpeedMultiplier);
            else
                _animal.PerformMove(direction.x, _path.SpeedMultiplier);
        }

        // 按当前段走向播放动画
        PlaySegmentAnimation();

        return State.Running;
    }

    public override void Reset()
    {
        _initialized = false;
        _finished = false;
        _progress = 0f;
        _lastAnim = null;
    }

    /// <summary>
    /// 初始化：把动物吸附到路径上离它最近的位置起步。
    /// </summary>
    private void Initialize()
    {
        _initialized = true;
        _progress = _path.NearestT((Vector2)_animal.transform.position);
    }

    /// <summary>
    /// 从 fromT 出发沿路径累计弧长，返回弧长 ≥ lookAhead 处的采样点。
    /// 循环路径在跨过末端时自动回卷到起点继续累计。
    /// </summary>
    private Vector2 LookAheadPoint(float fromT, float lookAhead)
    {
        int segments = _path.SegmentCount;
        if (segments <= 0)
            return _path.SamplePoint(fromT);

        float dt = 1f / (segments * LookAheadSampleSteps);
        float t = fromT;
        Vector2 prev = _path.SamplePoint(t);
        float accumulated = 0f;
        int guard = 0;
        const int maxSteps = 500; // 防死循环上限

        while (guard++ < maxSteps)
        {
            t += dt;
            if (t >= 1f)
            {
                if (_path.Loop)
                    t -= 1f; // 循环路径：回卷到起点继续
                else
                    return _path.SamplePoint(1f); // 非循环：返回末端
            }

            Vector2 cur = _path.SamplePoint(t);
            accumulated += Vector2.Distance(prev, cur);
            prev = cur;

            if (accumulated >= lookAhead)
                return cur;
        }

        return _path.SamplePoint(t);
    }

    /// <summary>
    /// 移动速度：优先从移动委托侧无法获知，这里用 FishPath 倍率 × 动物游泳速度；
    /// 仅用于前瞻距离估算，无法获取时回退 ArriveRadius。
    /// </summary>
    private float GetMovementSpeed()
    {
        float baseSpeed = 1f;
        if (_animal is BubbleFishAI fish)
            baseSpeed = fish.SwimSpeed;

        return baseSpeed * _path.SpeedMultiplier;
    }

    private void PlaySegmentAnimation()
    {
        if (_animResolver == null)
            return;

        int segmentIndex = _path.SegmentIndexAt(_progress);
        if (segmentIndex < 0)
            return;

        string anim = _animResolver(_path.GetSegmentDirection(segmentIndex));
        if (anim != _lastAnim)
        {
            _lastAnim = anim;
            _animal.PlayAnimation(anim);
        }
    }
}
