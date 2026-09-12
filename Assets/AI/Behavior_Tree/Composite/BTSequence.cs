using System.Collections.Generic;

/// <summary>
/// [BT] 顺序节点：依次执行子节点，全部 Success 才返回 Success。
/// 任一子节点 Failure 立即失败并重置；子节点 Running 时记住进度，下帧继续。
/// 数据驱动：工厂创建后通过 AddChild 逐个挂子节点。
/// </summary>
[BTNode("Sequence")]
public class BTSequence : BTNode
{
    private readonly List<BTNode> _children;
    private int _currentIndex;

    public BTSequence(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    /// <summary>工厂用：创建空顺序节点后挂子节点。</summary>
    public BTSequence() : this(new BTNode[0]) { }

    /// <summary>追加子节点（链式）。</summary>
    public BTNode AddChild(BTNode child)
    {
        if (child != null) _children.Add(child);
        return this;
    }

    public override IReadOnlyList<BTNode> Children => _children;

    protected override State DoTick()
    {
        while (_currentIndex < _children.Count)
        {
            State result = _children[_currentIndex].Tick();

            switch (result)
            {
                case State.Running:
                    return State.Running;
                case State.Failure:
                    Reset();
                    return State.Failure;
                case State.Success:
                    _currentIndex++;
                    break;
            }
        }

        Reset();
        return State.Success;
    }

    public override void Reset()
    {
        _currentIndex = 0;
        foreach (BTNode child in _children)
            child.Reset();
    }
}
