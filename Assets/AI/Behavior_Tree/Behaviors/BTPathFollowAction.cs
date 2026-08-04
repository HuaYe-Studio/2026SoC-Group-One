using UnityEngine;

/// <summary>
/// [BT] 通用路径跟随节点：驱动动物沿 FishPath 样条路径平滑游动。
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

    private float _progress;   // 沿路径的全局进度 t∈[0,1]
    private bool _initialized;
    private bool _finished;    // 已到达终点（非循环路径）
    private string _lastAnim;

    /// <param name="move">移动委托(direction, speedMultiplier)，null 时回退为水平 PerformMove</param>
    /// <param name="animResolver">动画解析委托(segmentDirection)->动画名，null 时按走向判定 SwimUp/SwimDown/SwimForward</param>
    public BTPathFollowAction(AnimalBase animal, FishPath path,
        System.Action<Vector2, float> move = null,
        System.Func<Vector2, string> animResolver = null)
    {
        _animal = animal;
        _path = path;
        _move = move;
        _animResolver = animResolver;
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

        // 当前目标点（样条采样）
        Vector2 target = _path.SamplePoint(_progress);
        Vector2 toTarget = target - (Vector2)_animal.transform.position;

        // 接近目标点 → 沿路径推进进度
        if (toTarget.magnitude <= _path.ArriveRadius)
        {
            if (!Advance())
            {
                _animal.StopMoving();
                return State.Running; // 终点且不循环
            }
            target = _path.SamplePoint(_progress);
            toTarget = target - (Vector2)_animal.transform.position;
        }

        // 游向目标
        Vector2 direction = toTarget.normalized;
        if (_move != null)
            _move(direction, _path.SpeedMultiplier);
        else
            _animal.PerformMove(direction.x, _path.SpeedMultiplier);

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
    /// 沿路径推进进度。推进量 = 到达半径 / 总长，保证步进细腻平滑。
    /// 返回 false 表示到达终点且不可循环。
    /// </summary>
    private bool Advance()
    {
        float step = _path.TotalLength > 0f
            ? _path.ArriveRadius / _path.TotalLength
            : 0f;

        _progress += step;

        if (_progress >= 1f)
        {
            if (_path.Loop)
            {
                _progress -= 1f;
                return true;
            }
            _progress = 1f;
            _finished = true;
            return false;
        }

        return true;
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
