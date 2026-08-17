using UnityEngine;

/// <summary>
/// [BT] 连跳组节点（青蛙觅食专用）：一次行为执行"一组连续跳跃"。
/// 真实感：组内每次跳跃高度递减（越跳越矮）、落地后间隔递减（越跳越快），
/// 模仿青蛙受惊/觅食时急促的连跳，而不是每次跳完长时间休息。
/// 参数化回退：将 _jumpsPerBurst 设为 1 时行为等价于旧的"单跳"节奏。
/// 防误判：以"离地→着地"上升沿计数，起跳后必须离过地才算完成一次跳跃，
/// 避免着地瞬间重复触发下一跳（二段跳误判）。
/// 注意：Reset() 只重置内部状态，不调用物理副作用。
/// </summary>
public class BTBurstHopAction : BTNode
{
    private readonly FrogAI _frog;
    private readonly System.Func<float> _directionProvider;
    private readonly System.Func<float, float> _directionSteer; // 可选：起跳方向修正委托（同类分离等），null 不修正
    private readonly System.Func<float, float> _speedScale;     // 可选：按方向缩放跳跃距离（伤害源回避等），null 不缩放
    private readonly float _baseSpeedMultiplier;
    private readonly int _jumpsPerBurst;
    private readonly float _heightDecay;      // 每次跳跃高度衰减系数（<1 = 越跳越矮）
    private readonly float _intervalDecay;    // 落地后间隔衰减系数（<1 = 越跳越快）
    private readonly float _baseInterval;     // 第一次落地后到下一次起跳的基础间隔（秒）
    private readonly float _timeout;

    // ---- 内部状态 ----
    private int _jumpsDone;                  // 本组已完成的跳跃数
    private bool _hasStarted;                 // 是否已开始（等着地）
    private bool _hasLeftGround;              // 当前这跳是否已离地
    private bool _wasGroundedLastFrame;
    private float _nextHopTime;
    private float _startTime;

    /// <summary>
    /// 连跳组节点。
    /// </summary>
    /// <param name="frog">青蛙实例</param>
    /// <param name="directionProvider">方向提供器（如：() => Random.value < 0.5f ? 1f : -1f）</param>
    /// <param name="baseSpeedMultiplier">基础速度倍率（第 0 跳）</param>
    /// <param name="jumpsPerBurst">每组连跳次数（=1 时退化为单跳）</param>
    /// <param name="heightDecay">高度衰减系数 0~1，默认 0.8（每跳降低 20%）</param>
    /// <param name="intervalDecay">间隔衰减系数 0~1，默认 0.7（每跳加快 30%）</param>
    /// <param name="baseInterval">落地后到下一次起跳的基础间隔（秒）</param>
    /// <param name="timeout">整组超时（秒），防止卡死</param>
    /// <param name="directionSteer">可选：起跳方向修正委托（如同类分离），入参导航方向、返回修正后方向；null 不修正</param>
    /// <param name="speedScale">可选：按方向缩放跳跃距离（如伤害源回避），入参最终方向、返回速度缩放系数；null 不缩放</param>
    public BTBurstHopAction(FrogAI frog, System.Func<float> directionProvider,
        float baseSpeedMultiplier = 1f, int jumpsPerBurst = 3,
        float heightDecay = 0.8f, float intervalDecay = 0.7f,
        float baseInterval = 0.15f, float timeout = 5f,
        System.Func<float, float> directionSteer = null,
        System.Func<float, float> speedScale = null)
    {
        _frog = frog;
        _directionProvider = directionProvider;
        _directionSteer = directionSteer;
        _speedScale = speedScale;
        _baseSpeedMultiplier = baseSpeedMultiplier;
        _jumpsPerBurst = Mathf.Max(1, jumpsPerBurst);
        _heightDecay = Mathf.Clamp01(heightDecay);
        _intervalDecay = Mathf.Clamp01(intervalDecay);
        _baseInterval = baseInterval;
        _timeout = timeout;
    }

    public override State Tick()
    {
        // 首次进入：必须落地才开始（空中进入则等待）
        if (!_hasStarted)
        {
            if (!_frog.IsGrounded)
                return State.Running;

            _hasStarted = true;
            _startTime = Time.time;
            _wasGroundedLastFrame = true;
            _nextHopTime = 0f;
        }

        // 超时兜底：整组未完成（如卡在低矮空间跳不起来）→ 放弃本次，防死循环
        if (Time.time - _startTime > _timeout)
        {
            Reset();
            return State.Failure;
        }

        bool grounded = _frog.IsGrounded;
        bool justLanded = grounded && !_wasGroundedLastFrame;
        _wasGroundedLastFrame = grounded;

        // 着地上升沿：完成一次跳跃计数
        if (justLanded)
        {
            _hasLeftGround = false;
            _jumpsDone++;

            // 组内跳数已满 → 整组完成
            if (_jumpsDone >= _jumpsPerBurst)
            {
                Reset();
                return State.Success;
            }

            // 安排下一次起跳：落地后间隔递减（频率递增）
            float interval = _baseInterval * Mathf.Pow(_intervalDecay, _jumpsDone - 1);
            _nextHopTime = Time.time + interval;
        }

        // 起跳条件：未在跳跃中 + 已落地 + 到起跳时间 + 组内还有剩余跳数
        if (!_hasLeftGround && grounded && Time.time >= _nextHopTime && _jumpsDone < _jumpsPerBurst)
        {
            // 高度递减：第 n 跳高度 = 基础 × decay^n
            float heightMultiplier = Mathf.Pow(_heightDecay, _jumpsDone);
            float direction = _directionProvider?.Invoke() ?? 1f;
            // 可选起跳方向修正（同类分离）：叠加在导航方向上，避免扎堆
            if (_directionSteer != null)
                direction = _directionSteer(direction);
            // 可选距离缩放（伤害源回避等）：按最终方向缩放跳跃距离
            float speedScale = _speedScale != null ? _speedScale(direction) : 1f;
            _frog.PerformHop(direction, _baseSpeedMultiplier * heightMultiplier * speedScale);
            _hasLeftGround = true; // 标记本次已起跳，等离地再落地才算完成
        }

        return State.Running;
    }

    /// <summary>整组完成或被打断时由行为树调用。只做状态清理。</summary>
    public override void Reset()
    {
        _jumpsDone = 0;
        _hasStarted = false;
        _hasLeftGround = false;
        _wasGroundedLastFrame = false;
        _nextHopTime = 0f;
        _startTime = 0f;
    }
}
