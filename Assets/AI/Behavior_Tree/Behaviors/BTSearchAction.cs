using UnityEngine;

/// <summary>
/// [BT] 搜索节点：前往 Blackboard 中记录的玩家最后已知位置进行搜索。
/// 到达目标位置后削减威胁值并返回 Success；记忆过期或威胁消退时返回 Failure。
/// 模拟生物"刚才这里有动静，过去看看"的警戒行为。
/// </summary>
public class BTSearchAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly Blackboard _bb;
    private readonly float _arriveDistance;
    private readonly float _speedMultiplier;

    private bool _hasMoved;
    private bool _hasLeftGround;

    /// <param name="animal">动物实例</param>
    /// <param name="arriveDistance">到达判定距离（米）</param>
    /// <param name="speedMultiplier">搜索移动速度倍率</param>
    public BTSearchAction(AnimalBase animal, float arriveDistance = 1f, float speedMultiplier = 1.2f)
    {
        _animal = animal;
        _bb = animal.Board;
        _arriveDistance = arriveDistance;
        _speedMultiplier = speedMultiplier;
    }

    public override State Tick()
    {
        // 记忆过期或威胁已消退 → 放弃搜索
        if (!_bb.HasThreatMemory || _bb.ThreatLevel <= 10f)
        {
            Reset();
            return State.Failure;
        }

        // 到达最后已知位置 → 削减威胁值，搜索完成
        float dist = Vector2.Distance(_animal.transform.position, _bb.LastKnownPlayerPos);
        if (dist <= _arriveDistance)
        {
            _bb.ThreatLevel = Mathf.Max(_bb.ThreatLevel - 20f, 0f);
            Reset();
            return State.Success;
        }

        // 追踪是否已经离地
        if (!_animal.IsGrounded)
            _hasLeftGround = true;

        // 着地后允许起跳下一段（支持多段搜索移动）
        if ((!_hasMoved || _hasLeftGround) && _animal.IsGrounded)
        {
            float direction = Mathf.Sign(_bb.LastKnownPlayerPos.x - _animal.transform.position.x);
            _animal.PerformMove(direction, _speedMultiplier);
            _hasMoved = true;
            _hasLeftGround = false;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _hasMoved = false;
        _hasLeftGround = false;
    }
}
