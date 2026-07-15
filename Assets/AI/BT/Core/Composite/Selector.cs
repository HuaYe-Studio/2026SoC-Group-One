using System.Collections.Generic;

/// <summary>
/// [BT] 选择节点（OR）：从左到右 Tick 子节点，返回第一个非 Failure 的结果。
/// 相当于 "或" 逻辑——任意一个子节点成功或运行中即可。
/// </summary>
public class Selector : BTNode
{
    private readonly List<BTNode> _children = new List<BTNode>();
    private int _currentIndex;

    public Selector(List<BTNode> children)
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
                case State.Success:
                    return State.Success;
                case State.Failure:
                    _currentIndex++;
                    break;
            }
        }

        return State.Failure;
    }
}
