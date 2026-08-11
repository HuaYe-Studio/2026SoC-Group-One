using UnityEngine;

/// <summary>
/// [BT] 追捕玩家节点：朝玩家方向持续移动（支持蜘蛛8向爬行的垂直方向）。
/// 玩家丢失、超出放弃距离、或玩家变为同形态（蜘蛛）时 → Failure；贴近玩家 → Success。
/// </summary>
public class BTChasePlayerAction : BTNode
{
    private readonly SpiderAI _spider;
    private readonly AnimalBase _animal;
    private readonly Blackboard _bb;
    private readonly float _speedMultiplier;
    private readonly float _arriveRadius;
    private readonly float _timeout;

    private bool _hasStarted;
    private float _startTime;

    /// <param name="speedMultiplier">追捕速度倍率</param>
    /// <param name="arriveRadius">贴近判定距离（米）</param>
    /// <param name="timeout">超时秒数：开始追捕后此时间内未贴近则放弃（防不可达目标无限追击）</param>
    public BTChasePlayerAction(SpiderAI spider, float speedMultiplier = 1.2f, float arriveRadius = 0.8f, float timeout = 6f)
    {
        _spider = spider;
        _animal = spider;
        _bb = spider.Board;
        _speedMultiplier = speedMultiplier;
        _arriveRadius = arriveRadius;
        _timeout = timeout;
    }

    public override State Tick()
    {
        // 进入节点即开始计时（首次进入时）
        if (!_hasStarted)
        {
            _hasStarted = true;
            _startTime = Time.time;
        }

        // 玩家丢失、超出放弃距离、或玩家变为同形态（蜘蛛）友好 → 放弃追击
        if (!_bb.IsPlayerVisible || _bb.PlayerDistance > _spider.AbandonChaseDistance || _bb.IsPlayerSameForm)
        {
            Reset();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            return State.Failure;
        }

        // 超时未贴近 → 放弃（玩家可能在无法到达的位置）
        if (Time.time - _startTime > _timeout)
        {
            Reset();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            return State.Failure;
        }

        // 贴近玩家 → 追捕完成（后续交给更高层判定，如捕食/攻击）
        if (_bb.PlayerDistance <= _arriveRadius)
        {
            Reset();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            return State.Success;
        }

        // 持续朝玩家完整方向（含垂直）追捕
        _spider.ChasePlayer(_speedMultiplier);
        return State.Running;
    }

    public override void Reset()
    {
        _hasStarted = false;
        _startTime = 0f;
    }
}
