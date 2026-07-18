/// <summary>
/// [BT] 行为树节点基类：所有节点每帧通过 Tick() 返回执行结果。
/// 统一使用 BT 前缀命名，避免与 DOTween 的 Sequence 等类型冲突。
/// </summary>
public abstract class BTNode
{
    /// <summary>
    /// 节点执行结果三态。
    /// </summary>
    public enum State
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// 每帧执行一次，返回当前节点状态。
    /// </summary>
    public abstract State Tick();

    /// <summary>
    /// 被高优先级分支打断或重新开始时调用，重置内部状态。
    /// </summary>
    public virtual void Reset() { }
}
