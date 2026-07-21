using UnityEngine;

/// <summary>
/// [BT] 游动节点：在出生点周围以较长的"一段距离"来回巡游，带轻微上下漂移。
/// 靠近出生点时随机挑方向游出去 2~4 秒，走远了再折返，避免短程来回抽搐。
/// </summary>
public class BTSwimAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _driftInterval;

    private float _driftTimer;
    private float _driftDirectionY;
    private float _swimDirection = 1f;
    private float _directionLockUntil;

    /// <param name="animal">动物实例</param>
    /// <param name="driftInterval">漂移方向切换间隔（秒）</param>
    public BTSwimAction(AnimalBase animal, float driftInterval = 1.5f)
    {
        _animal = animal;
        _driftInterval = driftInterval;
    }

    public override State Tick()
    {
        // 玩家靠近时不做巡游，让 Flee 分支单独接管，避免巡游和逃跑打架
        if (_animal.IsPlayerDetected && _animal.PlayerDistance <= _animal.FleeSafeDistance)
            return State.Failure;

        // 垂直漂移
        _driftTimer += Time.deltaTime;
        if (_driftTimer >= _driftInterval)
        {
            _driftTimer = 0f;
            _driftDirectionY = Random.Range(-1f, 1f);
        }

        // 方向锁到期 → 重新决定游动方向
        if (Time.time >= _directionLockUntil)
        {
            Vector2 toSpawn = _animal.SpawnPosition - (Vector2)_animal.transform.position;

            if (toSpawn.magnitude > _animal.PatrolRadius)
            {
                // 超出巡游半径 → 折返，短锁 1~2s（让它先游回来）
                _swimDirection = Mathf.Sign(toSpawn.x);
                _directionLockUntil = Time.time + Random.Range(1f, 2f);
            }
            else if (toSpawn.magnitude < 1f)
            {
                // 在出生点附近 → 随机挑个方向游远，长锁 2~4s（走远一点再考虑回头）
                _swimDirection = Random.value < 0.5f ? 1f : -1f;
                _directionLockUntil = Time.time + Random.Range(2f, 4f);
            }
            else
            {
                // 中间地带 → 保持当前方向继续，中锁 1.5~2.5s
                _directionLockUntil = Time.time + Random.Range(1.5f, 2.5f);
            }
        }

        _animal.PerformMove(_swimDirection);

        return State.Running;
    }

    public override void Reset()
    {
        _driftTimer = 0f;
        _driftDirectionY = 0f;
        _directionLockUntil = 0f;
    }
}
