using UnityEngine;

/// <summary>
/// [BT] 自由巡游节点：随机方向换向 + 碰墙反向，超出范围偏向出生点。
/// 垂直漂移由 BubbleFishAI.PerformMove 内部统一处理。持续 Running。
/// </summary>
public class BTWanderAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly float _swimRange;
    private float _swimDirection = 1f;
    private float _nextTurn;
    private float _nextWallCheck;
    private const float WallCheckInterval = 0.3f;

    /// <param name="swimRange">巡游范围半径（米），超出后会在换向时偏向出生点</param>
    public BTWanderAction(AnimalBase animal, float swimRange = 6f)
    {
        _animal = animal;
        _swimRange = swimRange;
    }

    public override State Tick()
    {
        if (Time.time >= _nextTurn)
        {
            Vector2 toSpawn = _animal.SpawnPosition - (Vector2)_animal.transform.position;

            if (toSpawn.magnitude > _swimRange)
            {
                // 超出范围 → 偏向出生点方向
                _swimDirection = Mathf.Sign(toSpawn.x);
            }
            else
            {
                _swimDirection = Random.value < 0.5f ? 1f : -1f;
            }

            _nextTurn = Time.time + Random.Range(1.5f, 3f);
        }

        if (Time.time >= _nextWallCheck)
        {
            _nextWallCheck = Time.time + WallCheckInterval;

            if (_animal.Board.IsWallAhead)
            {
                _swimDirection = -_swimDirection;
                _nextTurn = Time.time + Random.Range(1.5f, 3f);
            }
        }

        _animal.PerformMove(_swimDirection);
        return State.Running;
    }

    public override void Reset()
    {
        _nextTurn = 0f;
        _nextWallCheck = 0f;
    }
}
