using System.Collections.Generic;

/// <summary>
/// [BT] 选择节点：从上到下依次尝试子节点，任一子节点 Success 或 Running 即返回。
/// 每帧从头重新评估，高优先级分支就绪时会打断（Reset）正在运行的低优先级分支，
/// 保证"检测到玩家立即逃跑"这类抢占逻辑生效。
/// </summary>
public class BTSelector : BTNode
{
    private readonly List<BTNode> _children;
    private int _runningIndex = -1;

    public BTSelector(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override State Tick()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            State result = _children[i].Tick();

            if (result == State.Failure)
                continue;

            // 高优先级分支接管时，打断之前正在运行的低优先级分支
            if (_runningIndex != -1 && _runningIndex != i)
                _children[_runningIndex].Reset();

            _runningIndex = result == State.Running ? i : -1;
            return result;
        }

        // 全部失败：清理残留的运行分支
        if (_runningIndex != -1)
        {
            _children[_runningIndex].Reset();
            _runningIndex = -1;
        }

        return State.Failure;
    }

    public override void Reset()
    {
        _runningIndex = -1;
        foreach (BTNode child in _children)
            child.Reset();
    }
}
