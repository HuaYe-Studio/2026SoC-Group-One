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

    private bool _hasChased;

    /// <param name="animal">动物实例</param>
    /// <param name="chaseSpeedMultiplier">追击速度倍率</param>
    /// <param name="eatRange">吃到判定距离</param>
    public BTChaseAction(AnimalBase animal, float chaseSpeedMultiplier = 1.8f, float eatRange = 0.6f)
    {
        _animal = animal;
        _chaseSpeedMultiplier = chaseSpeedMultiplier;
        _eatRange = eatRange;
    }

    public override State Tick()
    {
        // 目标丢失或距离过远 → 放弃
        if (!_animal.IsFoodDetected || _animal.FoodDistance > _animal.DetectionRadius * 1.5f)
        {
            Reset();
            _animal.PlayAnimation("Idle");
            return State.Failure;
        }

        // 贴近目标 → 吃掉
        if (_animal.FoodDistance <= _eatRange)
        {
            EatFood();
            Reset();
            _animal.PlayAnimation("Idle");
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
    }

    /// <summary>
    /// 吃掉贴近范围内的食物GameObject。
    /// </summary>
    private void EatFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(_animal.transform.position, _eatRange);

        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Food"))
            {
                Object.Destroy(hit.gameObject);
                break;
            }
        }
    }
}
