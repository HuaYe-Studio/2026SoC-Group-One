/// <summary>
/// 状态接口，所有AI状态（Idle、Patrol、Flee等）需实现此接口。
/// 每个状态有 Enter / Update / Exit 三个生命周期方法，
/// 由 FSM 在对应时机调用。
/// </summary>
public interface IState
{
    /// <summary>
    /// 进入状态时调用一次，用于初始化状态参数。
    /// </summary>
    void OnEnter();

    /// <summary>
    /// 处于该状态时每帧调用，由 FSM.Update() 驱动。
    /// </summary>
    void OnUpdate();

    /// <summary>
    /// 离开状态时调用一次，用于清理或重置。
    /// </summary>
    void OnExit();
}
