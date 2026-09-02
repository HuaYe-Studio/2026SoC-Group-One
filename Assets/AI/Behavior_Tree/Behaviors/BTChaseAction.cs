using UnityEngine;

/// <summary>
/// [BT] 通用追猎节点：朝目标方向持续移动，贴近后吃掉返回 Success。
/// 支持多段追击（空中自动复位标记，落地后可继续追）。
/// </summary>
public class BTChaseAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _chaseSpeedMultiplier;
    private readonly float _eatRange;
    private readonly float _timeout;

    private bool _hasChased;
    private float _startTime;
    private bool _hasStarted;

    private static readonly Collider2D[] _hitBuffer = new Collider2D[16];

    /// <param name="animal">动物实例</param>
    /// <param name="chaseSpeedMultiplier">追击速度倍率</param>
    /// <param name="eatRange">吃到判定距离</param>
    /// <param name="timeout">超时秒数：开始追击后此时间内未吃到食物则返回 Failure（防止对不可达食物无限扑跳）</param>
    public BTChaseAction(AnimalBase animal, float chaseSpeedMultiplier = 1.8f, float eatRange = 0.6f, float timeout = 4f)
    {
        _animal = animal;
        _chaseSpeedMultiplier = chaseSpeedMultiplier;
        _eatRange = eatRange;
        _timeout = timeout;
    }

    protected override State DoTick()
    {
        // 进入节点即开始计时（首次进入时）
        if (!_hasStarted)
        {
            _hasStarted = true;
            _startTime = Time.time;
        }

        // 目标丢失或距离过远 → 放弃
        if (!_animal.IsFoodDetected || _animal.FoodDistance > _animal.DetectionRadius * 1.5f)
        {
            Reset();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            return State.Failure;
        }

        // 超时未吃到 → 放弃（食物可能在墙后/高台上不可达）
        if (Time.time - _startTime > _timeout)
        {
            Reset();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            return State.Failure;
        }

        // 贴近目标 → 吃掉
        if (_animal.FoodDistance <= _eatRange)
        {
            EatFood();
            Reset();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            return State.Success;
        }

        // 空中时重置标记，落地后允许再次追击（支持多段追扑）
        if (!_animal.IsGrounded)
            _hasChased = false;

        // 着地且尚未起跳 → 朝目标追击
        if (!_hasChased && _animal.IsGrounded)
        {
            float direction = Mathf.Sign(_animal.FoodDirection.x);
            _animal.PerformMove(direction, _chaseSpeedMultiplier);
            _hasChased = true;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _hasChased = false;
        _hasStarted = false;
        _startTime = 0f;
    }

    /// <summary>
    /// 吃掉贴近范围内的食物GameObject。
    /// </summary>
    private void EatFood()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(_animal.transform.position, _eatRange, _hitBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            if (_hitBuffer[i].CompareTag("Food"))
            {
                Object.Destroy(_hitBuffer[i].gameObject);
                break;
            }
        }
    }
}
