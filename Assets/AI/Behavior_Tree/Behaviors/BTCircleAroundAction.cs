using UnityEngine;

/// <summary>
/// [BT] 绕玩家巡游节点：以玩家为中心，沿切线方向弧线游动。
/// 方向缓慢变化（1~2.5s 重算一次），鱼自然绕到玩家另一侧，不被单侧卡死。
/// 玩家离开检测范围时返回 Failure，交由下级分支接管。
/// </summary>
public class BTCircleAroundAction : BTNode
{
    private readonly AnimalBase _animal;
    private float _direction;
    private float _nextRecompute;

    public BTCircleAroundAction(AnimalBase animal)
    {
        _animal = animal;
    }

    protected override State DoTick()
    {
        if (!_animal.IsPlayerDetected)
        {
            Reset();
            return State.Failure;
        }

        if (Time.time >= _nextRecompute)
        {
            Vector2 toPlayer = _animal.PlayerDirection;

            float tangent = Random.Range(-1f, 1f);
            float radial = Mathf.Sign(toPlayer.x) * Random.Range(-0.3f, 0.3f);
            _direction = Mathf.Clamp(tangent + radial, -1f, 1f);

            _nextRecompute = Time.time + Random.Range(1f, 2.5f);
        }

        _animal.PerformMove(_direction);
        return State.Running;
    }

    public override void Reset()
    {
        _nextRecompute = 0f;
    }
}
