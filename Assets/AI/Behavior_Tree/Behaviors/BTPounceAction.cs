using UnityEngine;

/// <summary>
/// [BT] 捕食节点：着地时朝食物方向扑跳（FROG_AnimState=4），落地后可继续追扑。
/// 贴近食物后吃掉返回 Success；食物丢失或距离过远返回 Failure。
/// </summary>
public class BTPounceAction : BTNode
{
    private readonly FrogAI _frog;

    private bool _hasPounced;

    private const float PounceSpeedMultiplier = 1.8f;
    private const float EatRange = 0.6f;

    public BTPounceAction(FrogAI frog)
    {
        _frog = frog;
    }

    public override State Tick()
    {
        // 食物丢失或距离过远 → 放弃
        if (!_frog.IsFoodDetected || _frog.FoodDistance > _frog.DetectionRadius * 1.5f)
        {
            Reset();
            _frog.PlayAnimation("Idle");
            return State.Failure;
        }

        // 贴近食物 → 吃掉
        if (_frog.FoodDistance <= EatRange)
        {
            EatFood();
            Reset();
            _frog.PlayAnimation("Idle");
            return State.Success;
        }

        // 空中时重置标记，落地后允许再次扑跳（支持多段追扑）
        if (!_frog.IsGrounded)
            _hasPounced = false;

        // 着地且尚未起跳 → 朝食物扑跳
        if (!_hasPounced && _frog.IsGrounded)
        {
            float direction = Mathf.Sign(_frog.FoodDirection.x);
            _frog.PerformHop(direction, PounceSpeedMultiplier, "Prey");
            _hasPounced = true;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _hasPounced = false;
    }

    /// <summary>
    /// 吃掉贴近范围内的食物GameObject。
    /// </summary>
    private void EatFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(_frog.transform.position, EatRange);

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
