using UnityEngine;

/// <summary>
/// [FSM] 捕食状态：动物朝检测到的食物猛扑过去。
/// 落地后若食物仍在附近则吃掉并返回闲置；若玩家出现则逃跑。
/// </summary>
public class PounceState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    private float _pounceDirection;
    private bool _hasPounced;

    private const float PounceSpeedMultiplier = 1.8f;
    private const float EatRange = 0.6f;

    public PounceState(FSM fsm, AnimalBase animal)
    {
        _fsm = fsm;
        _animal = animal;
    }

    public void OnEnter()
    {
        _hasPounced = false;

        // 面朝食物方向
        _pounceDirection = Mathf.Sign(_animal.FoodDirection.x);

        _animal.PlayAnimation("Prey");
    }

    public void OnUpdate()
    {
        // 玩家威胁始终优先
        if (_animal.IsPlayerDetected)
        {
            _fsm.ChangeState<FleeState>();
            return;
        }

        // 食物丢失 → 回闲置
        if (!_animal.IsFoodDetected)
        {
            _fsm.ChangeState<IdleState>();
            return;
        }

        // 距离过远放弃
        if (_animal.FoodDistance > _animal.DetectionRadius * 1.5f)
        {
            _fsm.ChangeState<IdleState>();
            return;
        }

        // 贴近食物 → 吃掉
        if (_animal.FoodDistance <= EatRange)
        {
            EatFood();
            _fsm.ChangeState<IdleState>();
            return;
        }

        // 还没起跳 → 起跳
        if (!_hasPounced)
        {
            _animal.PerformMove(_pounceDirection, PounceSpeedMultiplier);
            _hasPounced = true;
        }
    }

    public void OnExit()
    {
        _animal.StopMoving();
    }

    /// <summary>
    /// 吃掉最近的食物的GameObject。
    /// </summary>
    private void EatFood()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _animal.transform.position, EatRange);

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
