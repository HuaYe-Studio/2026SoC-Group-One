using UnityEngine;

/// <summary>
/// [BT] 自由巡游节点：目标点式巡游 + 平滑转向 + 障碍软避让 + 超出范围软吸引出生点。
/// - 在巡游半径内随机采样目标路点（水平坐标），到达后换新点，避免原地打转/来回抖；
/// - 路点采样带可达性校验（Physics2D.Raycast，可选障碍层），被挡时重新采样；
/// - 障碍软避让：前方扇形多射线探测，命中后转向障碍较少一侧并平滑绕行，
///   带冷却时间防振荡（避免贴墙左右抖死循环）；
/// - 超出范围时目标路点软吸引回中心 x，而非瞬间反向；
/// - 领地软约束（阶段四方案A）：与巡游中心（通常为领地中心）的 2D 距离超过
///   巡游半径时，强制面向中心并拉回路点，让非 A* 动物也倾向待在领地内；
/// - 方向由 heading 角度 + MoveTowardsAngle 限幅平滑转向，垂直漂移交给
///   BubbleFishAI.PerformMove 的 Perlin 噪声。持续 Running。
/// </summary>
public class BTWanderAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly Blackboard _bb;
    private readonly float _swimRange;
    private readonly LayerMask _obstacleMask;
    private readonly System.Func<float, float> _steer;               // 可选：移动方向修正委托（Boids 三力等），null 时不修正
    private readonly System.Func<Vector2> _centerProvider;           // 可选：巡游中心提供器（领地中心等），null 时用出生点
    private float _headingDeg;       // 当前游动方向角（度，0=右，180=左）
    private float _targetDeg;        // 目标方向角（度）
    private float _waypointX;        // 目标路点水平坐标（垂直由 PerformMove 噪声驱动）
    private float _nextWaypointPick; // 下次重新采样路点的时间
    private float _avoidCooldown;    // 避让冷却：冷却期间不再重复探测，防振荡

    // 节律状态：0=正常游动，1=冲刺，2=悬停
    private int _rhythmState;
    private float _rhythmEndTime;
    private string _lastRhythmAnim;  // 上次节律动画，仅在切换时播放一次

    private const float MaxTurnRate = 60f;         // 最大转角速率（度/秒）
    private const float WanderAngleRange = 50f;    // 目标角随机偏角范围（±°），形成轻微弧线
    private const float WaypointArriveRadius = 1.0f;   // 到达路点的水平判定半径（米）
    private const float WaypointResampleInterval = 4f; // 同一路点未达时的重采样间隔（秒）
    private const float ProbeDistance = 1.6f;      // 障碍探测距离（米）
    private const float ProbeAngleStep = 40f;      // 扇形探测的侧向角度步长（°）
    private const float AvoidCooldownDuration = 0.6f; // 避让冷却时长（秒）
    private const float RhythmMinInterval = 5f;    // 两次节律事件的最小间隔（秒）
    private const float RhythmMaxInterval = 9f;    // 两次节律事件的最大间隔（秒）
    private const float DashDuration = 0.8f;       // 冲刺时长（秒）
    private const float HoverDuration = 0.6f;      // 悬停时长（秒）
    private const float DashSpeedMultiplier = 1.5f;    // 冲刺速度倍率

    /// <param name="swimRange">巡游范围半径（米），超出后目标路点软吸引回中心</param>
    /// <param name="obstacleMask">路点可达性/障碍探测的障碍层（0 表示不检测）</param>
    /// <param name="steer">可选：移动方向修正委托（如 Boids 三力），入参导航水平方向、返回修正后方向；null 不修正</param>
    /// <param name="centerProvider">可选：巡游中心提供器（如领地中心），运行时每次查询；null 用出生点</param>
    public BTWanderAction(AnimalBase animal, float swimRange = 6f, LayerMask obstacleMask = default,
        System.Func<float, float> steer = null, System.Func<Vector2> centerProvider = null)
    {
        _animal = animal;
        _bb = animal != null ? animal.Board : null;
        _swimRange = swimRange;
        _obstacleMask = obstacleMask;
        _steer = steer;
        _centerProvider = centerProvider;
        _waypointX = _animal != null ? _animal.transform.position.x : 0f;
    }

    /// <summary>巡游中心：优先用领地中心（centerProvider），否则回退出生点。</summary>
    private Vector2 GetCenter()
    {
        return _centerProvider != null ? _centerProvider() : _animal.SpawnPosition;
    }

    public override State Tick()
    {
        Vector2 position = (Vector2)_animal.transform.position;
        Vector2 center = GetCenter();

        // 领地/巡游范围约束：2D 距离超出巡游半径 → 面向中心并拉回路点，强力回归；
        // 仅水平越界时沿用原逻辑（路点软吸引），避免 2D 边界处左右抖动
        if (Vector2.Distance(position, center) > _swimRange)
        {
            _waypointX = center.x;
            _targetDeg = center.x >= position.x ? 0f : 180f;
        }
        else if (Mathf.Abs(position.x - center.x) > _swimRange)
        {
            _waypointX = center.x;
        }

        // 到达路点或超时未达 → 重新采样新路点
        if (Time.time >= _nextWaypointPick || Mathf.Abs(_waypointX - position.x) <= WaypointArriveRadius)
            PickWaypoint();

        // 玩家避让：玩家可见且离鱼太近时，目标路点软偏移到远离玩家一侧，
        // 避免巡游目标恰在玩家上方导致"在玩家两侧往返荡"
        if (_bb != null && _bb.IsPlayerVisible && _bb.PlayerDistance < _bb.SafeRadius)
        {
            Vector2 playerPos = _bb.AnimalPosition + _bb.PlayerDirection * _bb.PlayerDistance;
            float awaySign = position.x >= playerPos.x ? 1f : -1f;
            _waypointX = position.x + awaySign * _bb.SafeRadius;
        }

        // 障碍软避让：扇形探测，命中后转向障碍较少一侧，带冷却防振荡
        CheckAvoid(_headingDeg);

        // 行为节律：定期触发冲刺/悬停，让游动观感更自然
        UpdateRhythm();

        // 悬停：停动 + Idle 动画
        if (_rhythmState == 2)
        {
            PlayRhythmAnim("Idle");
            _animal.StopMoving();
            return State.Running;
        }

        // 平滑转向：每帧最多转 MaxTurnRate·dt 度
        _headingDeg = Mathf.MoveTowardsAngle(_headingDeg, _targetDeg, MaxTurnRate * Time.deltaTime);

        // 水平方向 = cos(heading)，垂直漂移由 PerformMove 内部噪声处理
        // 冲刺时放大速度倍率并播放冲刺动画
        float speedMultiplier = _rhythmState == 1 ? DashSpeedMultiplier : 1f;
        if (_rhythmState == 1)
            PlayRhythmAnim("SwimForward");

        float direction = Mathf.Cos(_headingDeg * Mathf.Deg2Rad);
        // 可选方向修正（Boids 三力）：叠加在漫游导航方向上，只调方向、不改路点
        if (_steer != null)
            direction = _steer(direction);
        _animal.PerformMove(direction, speedMultiplier);
        return State.Running;
    }

    public override void Reset()
    {
        _nextWaypointPick = 0f;
        _avoidCooldown = 0f;
        _headingDeg = 0f;
        _targetDeg = 0f;
        _rhythmState = 0;
        _rhythmEndTime = 0f;
        _lastRhythmAnim = null;
        if (_animal != null)
            _waypointX = _animal.transform.position.x;
    }

    /// <summary>
    /// 节律调度：每 5~9s 随机进入冲刺（0.8s）或悬停（0.6s）状态，其余时间正常游动。
    /// 仅在状态切换时播放一次动画，避免每帧重复设置。
    /// </summary>
    private void UpdateRhythm()
    {
        if (Time.time < _rhythmEndTime)
            return;

        float roll = Random.value;
        if (roll < 0.35f)
        {
            _rhythmState = 1; // 冲刺
            _rhythmEndTime = Time.time + DashDuration;
        }
        else if (roll < 0.55f)
        {
            _rhythmState = 2; // 悬停
            _rhythmEndTime = Time.time + HoverDuration;
        }
        else
        {
            _rhythmState = 0; // 继续正常游动，仅重置调度间隔
        }

        if (_rhythmState == 0)
            _rhythmEndTime = Time.time + Random.Range(RhythmMinInterval, RhythmMaxInterval);
        else
            _lastRhythmAnim = null; // 进入新状态，允许动画重新播放
    }

    private void PlayRhythmAnim(string animName)
    {
        if (animName == _lastRhythmAnim)
            return;
        _lastRhythmAnim = animName;
        _animal.PlayAnimation(animName);
    }

    /// <summary>
    /// 在巡游半径内随机采样路点（水平坐标），做可达性校验，失败则重试数次。
    /// 成功时同步更新目标角（指向路点方向 + 随机微调形成弧线）。
    /// </summary>
    private void PickWaypoint()
    {
        _nextWaypointPick = Time.time + WaypointResampleInterval;
        Vector2 position = (Vector2)_animal.transform.position;
        float centerX = GetCenter().x;

        for (int i = 0; i < 6; i++)
        {
            float candidate = centerX + Random.Range(-_swimRange, _swimRange);
            if (!IsLineClear(position, new Vector2(candidate, position.y)))
                continue; // 水平段被障碍物阻挡 → 换一个点

            _waypointX = candidate;
            float baseDeg = candidate >= position.x ? 0f : 180f;
            _targetDeg = baseDeg + Random.Range(-WanderAngleRange, WanderAngleRange);
            return;
        }
        // 全部采样不可达：保持当前路点，等待下次重采样
    }

    /// <summary>
    /// 障碍软避让：正前方探测到障碍时，转向"障碍更少的一侧"。
    /// 冷却期间跳过探测，避免贴墙时左右反复变向（振荡）。
    /// </summary>
    private void CheckAvoid(float headingDeg)
    {
        if (Time.time < _avoidCooldown)
            return;

        // 正前方有障碍才触发避让
        if (!ProbeAngle(headingDeg, 0f))
            return;

        bool blockedLeft = ProbeAngle(headingDeg, -ProbeAngleStep);
        bool blockedRight = ProbeAngle(headingDeg, ProbeAngleStep);

        float turn;
        if (blockedLeft && !blockedRight)
            turn = ProbeAngleStep;        // 左侧也堵 → 向右绕
        else if (blockedRight && !blockedLeft)
            turn = -ProbeAngleStep;       // 右侧也堵 → 向左绕
        else
            turn = Random.value < 0.5f ? ProbeAngleStep : -ProbeAngleStep; // 两侧都堵 → 随机选一侧

        _targetDeg = Mathf.Repeat(headingDeg + turn, 360f);
        _avoidCooldown = Time.time + AvoidCooldownDuration;
        _nextWaypointPick = Time.time + WaypointResampleInterval;
    }

    /// <summary>
    /// 以当前方向角为基准，朝 offsetDeg 偏移方向探测障碍。
    /// </summary>
    private bool ProbeAngle(float headingDeg, float offsetDeg)
    {
        if (_obstacleMask.value == 0)
            return false;

        float rad = (headingDeg + offsetDeg) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        RaycastHit2D hit = Physics2D.Raycast(_animal.transform.position, dir, ProbeDistance, _obstacleMask);
        return hit.collider != null && !hit.collider.isTrigger;
    }

    /// <summary>
    /// 水平线段是否畅通（未被障碍层阻挡）。
    /// </summary>
    private bool IsLineClear(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f || _obstacleMask.value == 0)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(from, dir / dist, dist, _obstacleMask);
        return hit.collider == null || hit.collider.isTrigger;
    }
}
