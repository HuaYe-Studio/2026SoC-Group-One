using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [BT] 行为树节点基类：所有节点通过 Tick() 返回执行结果。
/// 统一使用 BT 前缀命名，避免与 DOTween 的 Sequence 等类型冲突。
///
/// 阶段 0 地基扩展（不改 Tick 语义）：
/// - 0.1 诊断字段：NodeName / LastState / TickCount / LastTickTime，供可视化/日志读取。
///      基类 Tick() 统一记录，子类只需实现 DoTick()（原逻辑原样搬移）。
/// - 0.2 子节点遍历：Children 只读接口，组合/装饰节点覆写暴露子节点，树可被任意遍历。
/// 外部驱动树的入口一律走 Tick()（记录诊断），节点内部调用子节点也走子节点 Tick()（递归记录）。
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

    // ---- 0.1 诊断字段（只读，不参与 Tick 语义）----
    private string _nodeName;
    private State _lastState;
    private int _tickCount;
    private float _lastTickTime;

    /// <summary>节点显示名：默认取类型名，可用 SetNodeName 覆盖（如 "分支1: 飞散"）。</summary>
    public string NodeName => string.IsNullOrEmpty(_nodeName) ? GetType().Name : _nodeName;

    /// <summary>最近一次 Tick 的返回结果（未 Tick 过为默认值 Failure）。</summary>
    public State LastState => _lastState;

    /// <summary>累计 Tick 次数（诊断/统计用）。</summary>
    public int TickCount => _tickCount;

    /// <summary>最近一次 Tick 的引擎时间（Time.time）。</summary>
    public float LastTickTime => _lastTickTime;

    /// <summary>全局 Tick 计数钩子开关（阶段 1.3）：关掉后 Tick() 不再累计 TickCount/LastTickTime（默认开）。</summary>
    public static bool CountTicks = true;

    /// <summary>节点状态变化事件（阶段 6.1/6.4）：LastState 变化时触发一次。
    /// BTDebugger（仅 Editor）订阅后记录时间线/输出变化日志；发布版无订阅者，空判一次零开销。</summary>
    public static event Action<BTNode> OnStateChanged;

    /// <summary>设置节点显示名（链式调用，便于构建树时给关键节点命名）。</summary>
    public BTNode SetNodeName(string name)
    {
        _nodeName = name;
        return this;
    }

    // ---- 0.2 子节点遍历接口（叶子节点默认无子节点）----
    /// <summary>子节点列表（只读）。叶子节点返回空；组合/装饰节点覆写暴露真实子节点。</summary>
    public virtual IReadOnlyList<BTNode> Children => Array.Empty<BTNode>();

    /// <summary>
    /// 每帧执行一次，记录诊断后返回当前节点状态。
    /// 所有外部调用（树驱动、节点互调）统一走此入口，保证诊断完整。
    /// </summary>
    public State Tick()
    {
        State result = DoTick();
        if (_lastState != result)
            OnStateChanged?.Invoke(this); // 阶段 6：状态变化钩子（时间线/日志数据源）
        _lastState = result;
        if (CountTicks)
        {
            _tickCount++;
            _lastTickTime = Time.time;
        }
        return result;
    }

    /// <summary>节点实际执行逻辑（原 Tick 实现搬移至此），子类覆写。</summary>
    protected abstract State DoTick();

    /// <summary>
    /// 被高优先级分支打断或重新开始时调用，重置内部状态。
    /// </summary>
    public virtual void Reset() { }
}
