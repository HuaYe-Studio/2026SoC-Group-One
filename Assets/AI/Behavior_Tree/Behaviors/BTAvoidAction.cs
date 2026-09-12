using UnityEngine;

/// <summary>
/// [BT] 轻回避节点：沿远离玩家方向游一小段距离，不跑远，只在近距离让出空间。
/// 完成后立即返回 Success，交由下级分支接管。
/// </summary>
public class BTAvoidAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _stepDistance;
    private Vector2 _startPos;
    private float _direction;

    public BTAvoidAction(AnimalBase animal, float stepDistance = 1.5f)
    {
        _animal = animal;
        _stepDistance = stepDistance;
    }

    protected override State DoTick()
    {
        if (_startPos == Vector2.zero)
        {
            _startPos = _animal.transform.position;
            _animal.PlayAnimation(AnimalAnimNames.Flee);

            _direction = -Mathf.Sign(_animal.PlayerDirection.x);
            if (Mathf.Abs(_direction) < 0.01f)
                _direction = Random.value < 0.5f ? -1f : 1f;
        }

        if (Vector2.Distance(_startPos, _animal.transform.position) >= _stepDistance)
        {
            _animal.StopMoving();
            _animal.PlayAnimation(AnimalAnimNames.Idle);
            _startPos = Vector2.zero;
            return State.Success;
        }

        _animal.PerformMove(_direction, _animal.FleeSpeedMultiplier);
        return State.Running;
    }

    public override void Reset()
    {
        _startPos = Vector2.zero;
    }
}
