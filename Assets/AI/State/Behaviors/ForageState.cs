using UnityEngine;

/// <summary>
/// 觅食状态：青蛙以跳跃方式在出生点周围觅食移动。
/// 每次落地后短暂停顿，连续跳跃若干次后切换至休息状态。
/// 若检测到玩家则切换至逃跑状态。
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
    private const int MaxHopsBeforeRest = 4;

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

        // 连续跳跃 4 次后休息
        if (_hopCount >= MaxHopsBeforeRest)
        {
            _fsm.ChangeState<RestState>();
            return;
        }

        // 跳跃后等待落地 + 短暂停顿，再起跳下一次
        if (_hasHopped && Time.time >= _landTime + PauseBetweenHops)
        {
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
