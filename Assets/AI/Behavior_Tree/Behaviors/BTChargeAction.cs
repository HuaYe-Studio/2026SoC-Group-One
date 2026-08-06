using UnityEngine;

/// <summary>
/// [BT] 冲撞攻击节点：玩家进入触发半径后朝玩家方向冲刺。
/// 命中玩家、撞墙、或冲撞超时后结束并进入冷却；冷却期间返回 Failure 让位给其他行为。
/// 依赖 SheepAI 提供的 StartCharge/StopCharge/IsCharging 原语。
/// </summary>
public class BTChargeAction : BTNode
{
    private readonly SheepAI _sheep;
    private readonly Blackboard _bb;

    private bool _hasCharged;
    private float _nextChargeTime;

    public BTChargeAction(SheepAI sheep)
    {
        _sheep = sheep;
        _bb = sheep.Board;
    }

    public override State Tick()
    {
        // 冷却中且未在冲撞 → 让位（由 Selector 走巡游等低优先级行为）
        if (Time.time < _nextChargeTime && !_sheep.IsCharging)
            return State.Failure;

        // 玩家丢失 → 结束冲撞
        if (!_bb.IsPlayerVisible)
        {
            _sheep.StopCharge();
            Reset();
            return State.Failure;
        }

        // 尚未进入冲撞：玩家在冲撞半径内 → 开始冲撞；否则朝玩家方向接近
        if (!_hasCharged)
        {
            if (_bb.PlayerDistance <= _sheep.ChargeTriggerRadius)
            {
                _hasCharged = true;
                _sheep.StartCharge(_bb.PlayerDirection.x);
            }
            else
            {
                // 复仇追猎阶段：朝玩家方向行走接近（远距离也能追击，进入半径后转冲撞）
                float direction = Mathf.Sign(_bb.PlayerDirection.x);
                _sheep.PerformMove(direction);
            }
            return State.Running;
        }

        // 命中玩家 → 结束并进入冷却
        if (_bb.PlayerDistance <= _sheep.ChargeHitRadius)
        {
            _sheep.StopCharge();
            _nextChargeTime = Time.time + _sheep.ChargeCooldown;
            Reset();
            return State.Success;
        }

        // 冲撞已结束（撞墙/超时，由 SheepAI.UpdateCharge 触发）→ 进入冷却
        if (!_sheep.IsCharging)
        {
            _nextChargeTime = Time.time + _sheep.ChargeCooldown;
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    /// <summary>重置冲撞标记。注意：冷却计时 _nextChargeTime 故意保留，跨重置持续生效。</summary>
    public override void Reset()
    {
        _hasCharged = false;
    }
}
