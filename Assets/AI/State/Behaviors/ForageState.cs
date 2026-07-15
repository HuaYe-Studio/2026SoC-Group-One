using UnityEngine;

/// <summary>
/// [FSM] 觅食状态：跳跃类动物在出生点周围跳跃移动。
/// 落地停顿后起跳，连续 MaxHopsBeforeRest 次后切休息。
/// 检测玩家→逃跑，检测食物→捕食。空中不计数。
/// </summary>
public class ForageState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    private float _hopDirection;
    private int _hopCount;
    private float _landTime;
    private bool _hasHopped;

    private const float PauseBetweenHops = 0.5f;
    private const int MaxHopsBeforeRest = 1;

    public ForageState(FSM fsm, AnimalBase animal)
    {
        _fsm = fsm;
        _animal = animal;
    }

    public void OnEnter()
    {
        _hopCount = 0;
        _hasHopped = false;
        StartNextHop();
    }

    public void OnUpdate()
    {
        // 玩家检测优先级最高
        if (_animal.IsPlayerDetected)
        {
            _fsm.ChangeState<FleeState>();
            return;
        }

        // 检测到食物 → 捕食
        if (_animal.IsFoodDetected)
        {
            _fsm.ChangeState<PounceState>();
            return;
        }

        // 跳跃后等待落地 + 短暂停顿，再判断下一跳或休息
        if (_hasHopped && _animal.IsGrounded && Time.time >= _landTime + PauseBetweenHops)
        {
            if (_hopCount >= MaxHopsBeforeRest)
            {
                _fsm.ChangeState<RestState>();
                return;
            }
            StartNextHop();
        }
    }

    public void OnExit()
    {
        _animal.StopMoving();
    }

    /// <summary>
    /// 选取一个随机方向，朝出生点附近跳跃。
    /// </summary>
    private void StartNextHop()
    {
        if (!_animal.IsGrounded)
            return;

        _hopCount++;

        // 偏向出生点方向，防止越走越远
        Vector2 toSpawn = _animal.SpawnPosition - (Vector2)_animal.transform.position;
        float biasDirection = Mathf.Sign(toSpawn.x);

        // 70% 概率朝出生点方向跳，30% 纯随机
        _hopDirection = Random.value < 0.7f ? biasDirection : (Random.value < 0.5f ? 1f : -1f);

        _animal.PerformMove(_hopDirection);
        _hasHopped = true;
        _landTime = Time.time;
    }
}
