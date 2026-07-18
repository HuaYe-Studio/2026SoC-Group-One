using UnityEngine;

/// <summary>
/// [BT] 跳跃节点：着地后朝出生点附近随机方向跳一次（FROG_AnimState=1），
/// 经历"离地 → 重新落地"后返回 Success。
/// 与 FSM ForageState 使用相同的防误判逻辑，防止地面检测过宽导致落地误判。
/// </summary>
public class BTHopAction : BTNode
{
    private readonly FrogAI _frog;

    private bool _hasHopped;
    private bool _hasLeftGround;

    public BTHopAction(FrogAI frog)
    {
        _frog = frog;
    }

    public override State Tick()
    {
        // 尚未起跳：必须先着地才允许跳
        if (!_hasHopped)
        {
            if (!_frog.IsGrounded)
                return State.Running;

            StartHop();
            return State.Running;
        }

        // 追踪是否已经离地
        if (!_frog.IsGrounded)
            _hasLeftGround = true;

        // 必须离过地 + 重新着地，才算真实落地
        if (_hasLeftGround && _frog.IsGrounded)
        {
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _hasHopped = false;
        _hasLeftGround = false;
    }

    /// <summary>
    /// 选取一个偏向出生点的随机方向起跳，防止越跳越远。
    /// </summary>
    private void StartHop()
    {
        Vector2 toSpawn = _frog.SpawnPosition - (Vector2)_frog.transform.position;
        float biasDirection = Mathf.Sign(toSpawn.x);

        // 70% 概率朝出生点方向跳，30% 纯随机
        float direction = Random.value < 0.7f ? biasDirection : (Random.value < 0.5f ? 1f : -1f);

        // PerformHop 内部会把 FROG_AnimState 设为 1（Jump）
        _frog.PerformHop(direction);
        _hasHopped = true;

        // 调试：确认起跳指令已执行及实际速度（问题定位后可删除）
        Debug.Log($"{_frog.name} BT起跳: 方向={direction} 速度={_frog.Rb.velocity}");
    }
}
