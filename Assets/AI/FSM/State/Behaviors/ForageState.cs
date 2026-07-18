using UnityEngine;

/// <summary>
/// [FSM] 觅食状态：跳跃类动物在出生点周围跳跃移动。
/// 落地后立刻切休息，每次只跳一次。检测玩家→逃跑，检测食物→捕食。
/// </summary>
public class ForageState : IState
{
    private readonly FSM _fsm;
    private readonly AnimalBase _animal;

    private float _hopDirection;
    private int _hopCount;
    private float _landTime;
    private bool _hasHopped;
    private bool _hasLeftGround;

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
        _hasLeftGround = false;
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

        // 尚未起跳：必须先着地才允许跳（防止空中进入该状态直接误判落地）
        if (!_hasHopped)
        {
            if (_animal.IsGrounded)
                StartNextHop();
            return;
        }

        // 追踪是否已经离地（防止地面检测过宽导致"落地"误判）
        if (!_animal.IsGrounded)
            _hasLeftGround = true;

        // 必须离过地 + 重新着地，才算真实落地
        if (_hasLeftGround && _animal.IsGrounded && Time.time > _landTime)
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
