using UnityEngine;

/// <summary>
/// [BT] 冲撞攻击节点：目标进入冲撞半径后朝目标冲刺。
/// 目标抽象化：通过目标提供者委托注入（默认 = 玩家），
/// 因此既可用于追玩家，也可用于追复仇目标（如羊复仇敌方 NPC）。
/// 命中目标、撞墙、或冲撞超时后结束并进入冷却；冷却期间返回 Failure 让位给其他行为。
/// 依赖 SheepAI 提供的 StartCharge/StopCharge/IsCharging 原语。
/// </summary>
public class BTChargeAction : BTNode
{
    private readonly SheepAI _sheep;
    private readonly Blackboard _bb;

    // 目标抽象：目标有效性判定 + 目标世界位置。不注入时默认取玩家。
    private readonly System.Func<bool> _hasTarget;
    private readonly System.Func<Vector2> _targetPos;

    private bool _hasCharged;
    private float _nextChargeTime;

    /// <param name="sheep">冲冲羊 AI</param>
    /// <param name="hasTarget">目标是否存在（为 null 时使用"玩家可见"作为默认判定）</param>
    /// <param name="targetPos">目标世界位置（为 null 时使用玩家位置作为默认目标）</param>
    public BTChargeAction(SheepAI sheep, System.Func<bool> hasTarget = null, System.Func<Vector2> targetPos = null)
    {
        _sheep = sheep;
        _bb = sheep.Board;
        _hasTarget = hasTarget ?? (() => _bb.IsPlayerVisible);
        _targetPos = targetPos ?? (() => (Vector2)_bb.LastKnownPlayerPos);
    }

    protected override State DoTick()
    {
        // 冷却中且未在冲撞 → 让位（由 Selector 走巡游等低优先级行为）
        if (Time.time < _nextChargeTime && !_sheep.IsCharging)
            return State.Failure;

        // 目标不存在（复仇目标已销毁 / 玩家不可见）→ 结束冲撞
        if (!_hasTarget())
        {
            _sheep.StopCharge();
            Reset();
            return State.Failure;
        }

        Vector2 animalPos = (Vector2)_sheep.transform.position;
        Vector2 target = _targetPos();
        float distance = Vector2.Distance(animalPos, target);

        // 尚未进入冲撞：目标在冲撞半径内 → 开始冲撞；否则朝目标方向接近
        if (!_hasCharged)
        {
            if (distance <= _sheep.ChargeTriggerRadius)
            {
                _hasCharged = true;
                float direction = target.x >= animalPos.x ? 1f : -1f;
                _sheep.StartCharge(direction);
            }
            else
            {
                // 追猎阶段：朝目标方向行走接近（远距离也能追击，进入半径后转冲撞）
                float direction = target.x >= animalPos.x ? 1f : -1f;
                _sheep.PerformMove(direction);
            }
            return State.Running;
        }

        // 命中目标 → 结束并进入冷却
        if (distance <= _sheep.ChargeHitRadius)
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
