using UnityEngine;

/// <summary>
/// [BT] 通用路径逃生节点：威胁紧迫时按威胁方向从多条逃生路径中选一条，沿路径快速撤离。
/// 画好的路径作为"方向指引"：鱼的实际逃生路线 = 画好路径的平行平移副本，
/// 从鱼当前所在位置（锚点）起步，方向与画好的路径完全一致。
/// 选择时机：进入节点时决策一次，此后每 redecideInterval 秒重新决策；
/// 重新决策时给「非当前路径」加偏好分，避免总在路径间来回切。
/// 威胁解除或路径走完 → Success，并把逃生起点写入 Blackboard 供回撤节点使用。
/// 通过委托解耦移动与动画（与 BTPathFollowAction 一致）。
/// </summary>
public class BTPathEscapeAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly FishPath[] _paths;
    private readonly Blackboard _bb;
    private readonly float _speedMultiplier;
    private readonly float _redecideInterval;
    private readonly float _newPathBias;
    private readonly System.Action<Vector2, float> _move;
    private readonly System.Func<Vector2, string> _animResolver;
    private readonly LayerMask _obstacleMask;

    // 回撤目标提供者：威胁解除后鱼要回到的位置。默认是逃生起点（威胁发生时的位置），
    // 可由外部指定（如"巡游路径的第 0 点"——从起点重新开始巡游）。
    private readonly System.Func<Vector2> _retreatTargetProvider;

    private FishPath _chosen;
    private float _progress;
    private bool _initialized;
    private bool _finished;
    private float _nextRedecideTime;
    private string _lastAnim;

    // 重复惩罚：同一条路径被连续重新选中的次数，次数越多惩罚越大。
    // 换路径时归零。用于降低"永远选同一条"的概率。
    private int _samePathStreak;
    private const float SamePathPenaltyPerStreak = 0.25f;

    // 路线平移锚点：画好的路径只作为"方向指引"，鱼的实际逃生路线 =
    // 画好路径的平行平移副本，从鱼当前所在位置（锚点）起步，方向与画好的路径完全一致。
    // 锚点同时作为回撤目标（Blackboard.RetreatTarget）。换路径时更新为鱼当前位置。
    private Vector2 _anchor;

    /// <summary>当前选中的逃生路径（供调试查看，null 表示尚未选中）。</summary>
    public FishPath CurrentPath => _chosen;

    /// <param name="speedMultiplier">逃生速度倍率（通常用动物的 FleeSpeedMultiplier）</param>
    /// <param name="redecideInterval">重新选路的间隔（秒），0 表示只在进入时选一次</param>
    /// <param name="newPathBias">重新决策时给非当前路径的偏好加分，越大越倾向换新路径</param>
    /// <param name="obstacleMask">障碍物层：被障碍物阻挡的逃生路线会被剔除（0 表示不检测）</param>
    /// <param name="retreatTargetProvider">回撤目标提供者：威胁解除后鱼要回到的位置（默认返回逃生起点）</param>
    public BTPathEscapeAction(AnimalBase animal, FishPath[] paths,
        float speedMultiplier = 1.5f, float redecideInterval = 1.5f, float newPathBias = 0.15f,
        System.Action<Vector2, float> move = null,
        System.Func<Vector2, string> animResolver = null,
        LayerMask obstacleMask = default,
        System.Func<Vector2> retreatTargetProvider = null)
    {
        _animal = animal;
        _paths = paths;
        _bb = animal.Board;
        _speedMultiplier = speedMultiplier;
        _redecideInterval = redecideInterval;
        _newPathBias = newPathBias;
        _move = move;
        _animResolver = animResolver;
        _obstacleMask = obstacleMask;
        _retreatTargetProvider = retreatTargetProvider;
    }

    public override State Tick()
    {
        // 威胁已解除 → 逃生完成并复位。重新进入时重新初始化、重记逃生起点。
        // 说明：Blackboard 已用最小持续时长迟滞保证 IsThreatUrgent 不会单帧抖动，
        //       此处 Reset 是安全的，能避免"二次逃生复用旧锚点/旧路径"的带毒状态。
        if (!_bb.IsThreatUrgent)
        {
            Reset();
            return State.Success;
        }

        if (_paths == null || _paths.Length == 0)
            return State.Failure;

        if (!_initialized)
            Initialize();

        // 没有任何可用路径（全部被挡 / 未配置）→ 逃生失败，回退到低优先级分支
        if (_chosen == null)
            return State.Failure;

        // 已走完（非循环路径）但威胁仍未解除：沿路径整体方向继续直线撤离，
        // 避免返回 Success 卡住 Selector（否则逃生分支每帧占着，鱼停在终点不动）。
        // 威胁解除后由上层切换到回撤分支，回到逃生起点。
        if (_finished)
            return ContinueFleeAfterFinish();

        // 固定间隔重新决策（倾向新路径）
        if (_redecideInterval > 0f && Time.time >= _nextRedecideTime)
        {
            SelectPath();
            _nextRedecideTime = Time.time + _redecideInterval;
        }

        // 沿路径推进（基于锚点平移采样：路线从鱼脚下起步，整体方向与画好的路径一致）
        Vector2 target = _chosen.SamplePoint(_progress, _anchor);
        Vector2 toTarget = target - (Vector2)_animal.transform.position;

        if (toTarget.magnitude <= _chosen.ArriveRadius)
        {
            // 走完当前路径（非循环）→ 威胁仍在，进入"沿方向继续撤离"状态
            if (!Advance())
                return ContinueFleeAfterFinish();
            target = _chosen.SamplePoint(_progress, _anchor);
            toTarget = target - (Vector2)_animal.transform.position;
        }

        Vector2 direction = toTarget.normalized;
        if (_move != null)
            _move(direction, _speedMultiplier);
        else
            _animal.PerformMove(direction.x, _speedMultiplier);

        PlaySegmentAnimation();

        return State.Running;
    }

    public override void Reset()
    {
        _initialized = false;
        _finished = false;
        _progress = 0f;
        _chosen = null;
        _nextRedecideTime = 0f;
        _lastAnim = null;
        _anchor = Vector2.zero;
        _samePathStreak = 0;
    }

    /// <summary>
    /// 逃生路径已走完但威胁仍未解除：沿路径整体方向继续直线撤离（不返回 Success，
    /// 否则 Selector 会被逃生分支每帧占住，鱼停在终点不动）。
    /// 威胁解除后由上层切换到回撤分支，回到逃生起点。
    /// </summary>
    private State ContinueFleeAfterFinish()
    {
        Vector2 direction = _chosen.Direction;
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
        return State.Running;
    }

    /// <summary>
    /// 首次进入：记录逃生起点（作为路线锚点与回撤目标），并选定第一条逃生路径。
    /// </summary>
    private void Initialize()
    {
        _initialized = true;

        // 逃生起点：路线平移锚点（路线从鱼脚下起步，方向不变）
        _anchor = (Vector2)_animal.transform.position;
        // 回撤目标：默认是逃生起点（鱼当前位置），可由外部指定（如巡游路径的第 0 点）
        _bb.RetreatTarget = _retreatTargetProvider != null ? _retreatTargetProvider() : _anchor;
        _bb.HasRetreatTarget = true;

        SelectPath();
        _nextRedecideTime = Time.time + _redecideInterval;
    }

    /// <summary>
    /// 按威胁方向从逃生路径中选一条：路径整体方向与威胁方向点积最大者胜出。
    /// 被障碍物阻挡的路线直接剔除（威胁方向 + 障碍物双重判断）。
    /// 当前正在走的路径受「重复惩罚」：连续被选中次数越多，惩罚越大，
    /// 给其他路径更多机会，避免鱼永远只走同一条。
    /// </summary>
    private void SelectPath()
    {
        Vector2 threatDir = -_bb.PlayerDirection; // 威胁方向 = 远离玩家
        Vector2 animalPos = (Vector2)_animal.transform.position;

        FishPath best = null;
        float bestScore = float.NegativeInfinity;

        // 当前路径的重复惩罚 = 连续选中次数 × 单次惩罚值
        // 第一次重选扣 0.25，第二次扣 0.5，第三次扣 0.75……方向分 max=1.0，换路门槛逐步降低
        float samePenalty = _samePathStreak * SamePathPenaltyPerStreak;

        foreach (FishPath path in _paths)
        {
            if (path == null || path.Points == null || path.Points.Count < 2)
                continue;

            // 障碍物判断：按锚点平移后的实际逃生路线被挡则放弃这条
            if (path.IsBlocked(animalPos, _obstacleMask))
                continue;

            // 用路径整体方向（归一化向量）与威胁方向匹配，不依赖路径绝对坐标
            Vector2 pathDir = path.Direction;
            float match = Vector2.Dot(pathDir, threatDir);

            // 评分 = 方向匹配度 + 新路径偏好 - 重复惩罚（仅当前路径）
            float score = match
                + (path == _chosen ? 0f : _newPathBias)
                - (path == _chosen ? samePenalty : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                best = path;
            }
        }

        // 全部路线都被障碍物阻挡时，best 仍为 null → 允许回退到当前路径继续走
        if (best == null)
            return;

        if (best != _chosen)
        {
            _chosen = best;
            _samePathStreak = 0;
            // 换路时路线改从鱼当前所在位置起步（平行平移），避免鱼往回游向旧锚点
            _anchor = animalPos;
            _progress = 0f;
            _finished = false;
        }
        else
        {
            // 同一路径再次被选中 → 累计重复次数
            _samePathStreak++;
        }
    }

    /// <summary>
    /// 沿当前路径推进进度。推进量 = 到达半径 / 总长。
    /// 返回 false 表示到达终点且不可循环（非循环逃生路径走完即止）。
    /// </summary>
    private bool Advance()
    {
        float step = _chosen.TotalLength > 0f
            ? _chosen.ArriveRadius / _chosen.TotalLength
            : 0f;

        _progress += step;

        if (_progress >= 1f)
        {
            if (_chosen.Loop)
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

        int segmentIndex = _chosen.SegmentIndexAt(_progress);
        if (segmentIndex < 0)
            return;

        string anim = _animResolver(_chosen.GetSegmentDirection(segmentIndex));
        if (anim != _lastAnim)
        {
            _lastAnim = anim;
            _animal.PlayAnimation(anim);
        }
    }
}
