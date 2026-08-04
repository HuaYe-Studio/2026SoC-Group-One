using UnityEngine;

/// <summary>
/// [BT] 调试装饰节点：包装任意节点，在内部节点状态发生变化时打印调试日志。
/// 用于定位行为树当前正停留在哪个分支/节点，以及状态如何流转。
/// 不影响被包裹节点的执行结果，仅用于调试。
/// 用法：new BTDebugNode("EscapePath", escapeNode, this)
/// </summary>
public class BTDebugNode : BTNode
{
    private readonly string _name;
    private readonly BTNode _inner;
    private readonly Object _context;
    private State _last = State.Failure;

    /// <param name="name">日志中显示的节点名（如分支名）</param>
    /// <param name="inner">被包裹的实际节点</param>
    /// <param name="context">日志前缀标识（通常是 MonoBehaviour，取 gameObject.name）</param>
    public BTDebugNode(string name, BTNode inner, Object context = null)
    {
        _name = name;
        _inner = inner;
        _context = context;
    }

    public override State Tick()
    {
        State result = _inner.Tick();

        // 仅在状态变化时打印，避免每帧刷屏
        if (result != _last)
        {
            _last = result;
            string prefix = _context != null ? _context.name + " " : "";
            Debug.Log($"[BT] {prefix}{_name} -> {result}");
        }

        return result;
    }

    public override void Reset()
    {
        _last = State.Failure;
        _inner.Reset();
    }
}
