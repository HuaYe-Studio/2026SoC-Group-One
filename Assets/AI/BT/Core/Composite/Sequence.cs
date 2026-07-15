using System.Collections.Generic;

/// <summary>
/// [BT] 顺序节点（AND）：从左到右 Tick 子节点，任一子节点失败则整体失败。
/// 相当于 "与" 逻辑——所有子节点必须成功才整体成功。
/// </summary>
public class Sequence : BTNode
{
    private readonly List<BTNode> _children = new List<BTNode>();
    private int _currentIndex;

    public Sequence(List<BTNode> children)
    {
        _children = children;
    }

    public override void OnEnter()
    {
        _currentIndex = 0;
    }

    public override State Tick()
    {
        while (_currentIndex < _children.Count)
        {
            State childState = _children[_currentIndex].Tick();

            switch (childState)
            {
                case State.Running:
                    return State.Running;
                case State.Failure:
                    return State.Failure;
                case State.Success:
                    _currentIndex++;
                    break;
            }
        }

        return State.Success;
    }
}
