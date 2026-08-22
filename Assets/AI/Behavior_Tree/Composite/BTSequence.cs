using System.Collections.Generic;

/// <summary>
/// [BT] 顺序节点：依次执行子节点，全部 Success 才返回 Success。
/// 任一子节点 Failure 立即失败并重置；子节点 Running 时记住进度，下帧继续。
/// </summary>
public class BTSequence : BTNode
{
    private readonly List<BTNode> _children;
    private int _currentIndex;

    public BTSequence(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override State Tick()
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
