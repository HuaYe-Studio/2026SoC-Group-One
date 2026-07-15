using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [FSM] 通用有限状态机，为动物AI行为提供状态管理基础。
/// 使用方式：先 RegisterState 注册状态，再 ChangeState 切换。
/// 使用泛型约束确保类型安全，编译期即可发现状态类型错误。
/// </summary>
public class FSM : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;

    private readonly Dictionary<Type, IState> _states = new Dictionary<Type, IState>();

    private IState _currentState;
    private Type _currentStateType;

    /// <summary>
    /// 当前活跃的状态实例，可用于外部查询当前状态。
    /// </summary>
    public IState CurrentState => _currentState;

    /// <summary>
    /// 当前活跃状态的类型。
    /// </summary>
    public Type CurrentStateType => _currentStateType;

    private void Update()
    {
        _currentState?.OnUpdate();
    }

    /// <summary>
    /// 注册一个状态。每个状态类型只能注册一次，重复注册会被忽略。
    /// </summary>
    /// <typeparam name="T">实现 IState 的状态类型</typeparam>
    /// <param name="state">状态实例</param>
    public void RegisterState<T>(T state) where T : IState
    {
        Type type = typeof(T);

        if (state == null)
        {
            Debug.LogWarning($"FSM: 尝试注册 null 状态 [{type.Name}]");
            return;
        }

        if (_states.ContainsKey(type))
        {
            Debug.LogWarning($"FSM: 状态 [{type.Name}] 已注册，忽略重复注册");
            return;
        }

        _states[type] = state;
    }

    /// <summary>
    /// 切换到指定类型的状态。自动触发旧状态的 OnExit 和新状态的 OnEnter。
    /// 如果目标状态与当前状态相同则忽略（不触发任何回调）。
    /// </summary>
    /// <typeparam name="T">目标状态类型</typeparam>
    public void ChangeState<T>() where T : IState
    {
        Type targetType = typeof(T);

        if (_currentStateType == targetType)
            return;

        if (!_states.TryGetValue(targetType, out IState newState))
        {
            Debug.LogError($"FSM: 状态 [{targetType.Name}] 未注册，请先调用 RegisterState");
            return;
        }

        Type oldType = _currentStateType;

        _currentState?.OnExit();

        _currentState = newState;
        _currentStateType = targetType;

        _currentState.OnEnter();

        if (_enableDebugLog)
            Debug.Log($"{gameObject.name} FSM: [{oldType?.Name ?? "None"}] → [{targetType.Name}]");
    }

    /// <summary>
    /// 强制切换到指定类型的状态，即使目标状态与当前状态相同也会先 Exit 再 Enter。
    /// 适用于需要重置当前状态的场景（如受到伤害后重新进入 Idle）。
    /// </summary>
    /// <typeparam name="T">目标状态类型</typeparam>
    public void ReenterState<T>() where T : IState
    {
        Type targetType = typeof(T);

        if (!_states.TryGetValue(targetType, out IState newState))
        {
            Debug.LogError($"FSM: 状态 [{targetType.Name}] 未注册，请先调用 RegisterState");
            return;
        }

        Type oldType = _currentStateType;

        _currentState?.OnExit();

        _currentState = newState;
        _currentStateType = targetType;

        _currentState.OnEnter();

        if (_enableDebugLog)
            Debug.Log($"{gameObject.name} FSM: [{oldType?.Name ?? "None"}] → [{targetType.Name}] (Reenter)");
    }

    /// <summary>
    /// 在控制台打印当前状态信息。不受 _enableDebugLog 开关限制，始终输出。
    /// </summary>
    public void LogCurrentState()
    {
        Debug.Log($"{gameObject.name} 当前状态: [{_currentStateType?.Name ?? "None"}]");
    }

    private void OnDisable()
    {
        _currentState?.OnExit();
        _currentState = null;
        _currentStateType = null;
    }
}
