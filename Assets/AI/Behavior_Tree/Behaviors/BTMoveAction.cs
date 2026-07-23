using UnityEngine;

/// <summary>
/// [BT] 通用移动节点：朝指定方向移动一次，直到着地后返回 Success。
/// 青蛙走 PerformHop，走路动物走 PerformMove，由各自动物的覆写自动分发。
/// </summary>
public class BTMoveAction : BTNode
{
    private readonly AnimalBase _animal;
    private readonly System.Func<float> _directionProvider;
    private readonly float _speedMultiplier;
    private readonly System.Func<bool> _isDebugLogEnabled;

    private bool _hasMoved;
    private bool _hasLeftGround;

    /// <summary>
    /// 通用移动节点。
    /// </summary>
    /// <param name="animal">动物实例</param>
    /// <param name="directionProvider">方向提供器（如：() => -Mathf.Sign(animal.PlayerDirection.x)）</param>
    /// <param name="speedMultiplier">速度倍率</param>
    /// <param name="isDebugLogEnabled">调试日志开关，可为null</param>
    public BTMoveAction(AnimalBase animal, System.Func<float> directionProvider,
        float speedMultiplier = 1f, System.Func<bool> isDebugLogEnabled = null)
    {
        _animal = animal;
        _directionProvider = directionProvider;
        _speedMultiplier = speedMultiplier;
        _isDebugLogEnabled = isDebugLogEnabled;
    }

    public override State Tick()
    {
        // 尚未移动：必须先着地才允许动
        if (!_hasMoved)
        {
            if (!_animal.IsGrounded)
                return State.Running;

            StartMove();
            return State.Running;
        }

        // 追踪是否已经离地
        if (!_animal.IsGrounded)
            _hasLeftGround = true;

        // 必须离过地 + 重新着地，才算真实落地
        if (_hasLeftGround && _animal.IsGrounded)
        {
            Reset();
            return State.Success;
        }

        return State.Running;
    }

    public override void Reset()
    {
        _hasMoved = false;
        _hasLeftGround = false;
    }

    private void StartMove()
    {
        float direction = _directionProvider?.Invoke() ?? 1f;
        _animal.PerformMove(direction, _speedMultiplier);
        _hasMoved = true;

        if (_isDebugLogEnabled != null && _isDebugLogEnabled())
            Debug.Log($"{_animal.name} BT移动: 方向={direction} 速度={_animal.Rb.velocity}");
    }
}
