/// <summary>
/// [BT] 行为树节点基类。所有节点（组合/装饰/条件/动作）继承此类。
/// 每个节点 Tick 返回三种状态：Success / Failure / Running。
/// </summary>
public abstract class BTNode
{
    public enum State
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// 执行节点逻辑，每帧由 BehaviorTreeRunner 驱动。
    /// </summary>
    public abstract State Tick();

    /// <summary>
    /// 进入节点时调用一次，用于重置内部状态。
    /// </summary>
    public virtual void OnEnter() { }

    /// <summary>
    /// 离开节点时调用一次，用于清理。
    /// </summary>
    public virtual void OnExit() { }
}
