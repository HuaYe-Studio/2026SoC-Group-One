#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [BT] 运行时状态采集器（阶段 1）：每帧从 BTTreeRegistry 取全部行为树，
/// DFS 遍历写入内部缓冲（复用 List、零分配），按采样间隔刷新"一帧内所有节点状态快照"。
///
/// - 1.1 主动采集：采集器每帧主动遍历读节点 LastState/TickCount，不侵入节点回调；
///       缓冲复用（List.Clear + 容量恒定），运行期零堆分配。
/// - 1.2 开关与频率：Enabled + SampleInterval（默认 0.2s）；整类 #if UNITY_EDITOR 包裹，
///       正式构建整体剥离，发布版零运行时开销。
/// - 6.1 状态时间线：订阅 BTNode.OnStateChanged，节点状态变化写入环形缓冲（容量 1024），
///       可回放"刚才这段时间树经历了什么"。
/// - 6.2 日志导出：ExportTimeline / 菜单 Tools/BT/Export Timeline 导出文本文件离线分析。
/// - 6.3 单帧暂停：Paused 只停采集（不动 timeScale），定格看某一帧所有节点状态。
/// - 6.4 状态变化日志：LogStateChanges 开启后节点状态变化实时输出 [树][节点名] 状态（默认关）。
/// - 验收：菜单 Tools/BT/Dump All Trees 或代码调用 Dump() / Dump(treeName)，
///       Console 直接打印任意树节点状态快照（阶段 2 可视化/Inspector 渲染的数据源）。
/// </summary>
public static class BTDebugger
{
    /// <summary>单个节点的一次快照（阶段 2 编辑器渲染的数据源）。</summary>
    public readonly struct NodeSnapshot
    {
        /// <summary>所属行为树名（注册名）。</summary>
        public readonly string TreeName;
        /// <summary>节点显示名。</summary>
        public readonly string NodeName;
        /// <summary>最近一次 Tick 结果。</summary>
        public readonly BTNode.State State;
        /// <summary>累计 Tick 次数。</summary>
        public readonly int TickCount;
        /// <summary>最近一次 Tick 的引擎时间。</summary>
        public readonly float LastTickTime;
        /// <summary>深度（根=0，用于缩进/层级渲染）。</summary>
        public readonly int Depth;
        /// <summary>本帧遍历序号（父先子后，同树内递增）。</summary>
        public readonly int Index;
        /// <summary>父节点 Index（根为 -1）。</summary>
        public readonly int ParentIndex;

        public NodeSnapshot(string treeName, string nodeName, BTNode.State state, int tickCount,
            float lastTickTime, int depth, int index, int parentIndex)
        {
            TreeName = treeName;
            NodeName = nodeName;
            State = state;
            TickCount = tickCount;
            LastTickTime = lastTickTime;
            Depth = depth;
            Index = index;
            ParentIndex = parentIndex;
        }
    }

    /// <summary>采集开关（默认开；关掉后 Tick 直接返回，保留缓存快照）。</summary>
    public static bool Enabled = true;

    /// <summary>采样间隔（秒）：每隔此时间刷新一次全树快照（默认 0.2s）。</summary>
    public static float SampleInterval = 0.2f;

    /// <summary>单帧暂停（阶段 6.3）：只停采集，不动 timeScale，定格最近一次快照。</summary>
    public static bool Paused;

    /// <summary>状态变化日志开关（阶段 6.4，默认关）：开启后节点状态变化实时输出 [树][节点名] 状态。</summary>
    public static bool LogStateChanges;

    /// <summary>状态时间线条目（阶段 6.1）。</summary>
    public readonly struct TimelineEntry
    {
        /// <summary>状态变化时刻（Time.time）。</summary>
        public readonly float Time;
        /// <summary>所属树名。</summary>
        public readonly string TreeName;
        /// <summary>节点显示名。</summary>
        public readonly string NodeName;
        /// <summary>变化后的新状态。</summary>
        public readonly BTNode.State State;

        public TimelineEntry(float time, string treeName, string nodeName, BTNode.State state)
        {
            Time = time;
            TreeName = treeName;
            NodeName = nodeName;
            State = state;
        }
    }

    /// <summary>时间线环形缓冲容量（超过后覆盖最旧条目）。</summary>
    public const int TimelineCapacity = 1024;

    private static readonly TimelineEntry[] _timeline = new TimelineEntry[TimelineCapacity];
    private static int _timelineCount;
    private static int _timelineWrite;

    static BTDebugger()
    {
        BTNode.OnStateChanged += OnNodeStateChanged; // 订阅状态变化（时间线/日志数据源）
    }

    private static void OnNodeStateChanged(BTNode node)
    {
        var entry = new TimelineEntry(Time.time, FindTreeOf(node), node.NodeName, node.LastState);
        PushTimeline(entry);
        if (LogStateChanges)
            Debug.Log($"[BT] {entry.TreeName} | {entry.NodeName} -> {entry.State}");
    }

    private static void PushTimeline(TimelineEntry entry)
    {
        _timeline[_timelineWrite] = entry;
        _timelineWrite = (_timelineWrite + 1) % TimelineCapacity;
        if (_timelineCount < TimelineCapacity) _timelineCount++;
    }

    /// <summary>时间线条目数（最多 TimelineCapacity）。</summary>
    public static int TimelineCount => _timelineCount;

    /// <summary>按时间顺序（旧→新）读取全部时间线条目（编辑器用，返回新数组）。</summary>
    public static TimelineEntry[] Timeline
    {
        get
        {
            var result = new TimelineEntry[_timelineCount];
            int start = (_timelineWrite - _timelineCount + TimelineCapacity) % TimelineCapacity;
            for (int i = 0; i < _timelineCount; i++)
                result[i] = _timeline[(start + i) % TimelineCapacity];
            return result;
        }
    }

    /// <summary>清空时间线。</summary>
    public static void ClearTimeline()
    {
        _timelineCount = 0;
        _timelineWrite = 0;
    }

    /// <summary>反查节点所属树名（遍历注册表 DFS，节点可能来自任意注册树；找不到返回 "?"）。</summary>
    private static string FindTreeOf(BTNode node)
    {
        var trees = BTTreeRegistry.Trees;
        for (int t = 0; t < trees.Count; t++)
        {
            if (trees[t] != null && ContainsNode(trees[t].Root, node))
                return trees[t].Name;
        }
        return "?";
    }

    private static bool ContainsNode(BTNode root, BTNode target)
    {
        if (root == null) return false;
        if (ReferenceEquals(root, target)) return true;
        var children = root.Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (ContainsNode(children[i], target)) return true;
        }
        return false;
    }

    /// <summary>
    /// 导出时间线为文本文件（阶段 6.2）：含时间/树/节点/状态，可离线分析。
    /// </summary>
    public static void ExportTimeline(string filePath)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine($"[BTDebugger] 时间线导出 time={Time.time:F2} 条目={_timelineCount}/{TimelineCapacity}");
        sb.AppendLine("时间\t树\t节点\t状态");
        foreach (var e in Timeline)
            sb.AppendLine($"{e.Time:F2}\t{e.TreeName}\t{e.NodeName}\t{e.State}");
        System.IO.File.WriteAllText(filePath, sb.ToString());
        Debug.Log($"[BTDebugger] 时间线已导出: {filePath}（{_timelineCount} 条）");
    }

    private static readonly List<NodeSnapshot> _buffer = new List<NodeSnapshot>(256);
    private static float _nextSampleTime;
    private static int _version;
    private static Driver _driver;

    /// <summary>最近一次采样快照（只读；阶段 2 编辑器侧直接读取渲染）。</summary>
    public static IReadOnlyList<NodeSnapshot> Snapshots => _buffer;

    /// <summary>快照版本号：每次采样 +1，编辑器侧据此检测新快照。</summary>
    public static int Version => _version;

    /// <summary>采样已启用的树数量（快照内不同 TreeName 计数）。</summary>
    public static int TreeCountInSnapshot
    {
        get
        {
            if (_buffer.Count == 0) return 0;
            int count = 1;
            for (int i = 1; i < _buffer.Count; i++)
                if (_buffer[i].TreeName != _buffer[i - 1].TreeName)
                    count++;
            return count;
        }
    }

    /// <summary>播放时自动挂载每帧驱动（隐式 GameObject，DontDestroyOnLoad）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDriver()
    {
        if (_driver != null) return;
        var go = new GameObject("BTDebuggerDriver");
        Object.DontDestroyOnLoad(go);
        _driver = go.AddComponent<Driver>();
    }

    /// <summary>
    /// 主动采集一次（到采样点才真正刷新；Editor 窗口可调用来强制采样）。
    /// 每帧由隐式 Driver 调用；采集逻辑仅 Editor 编译，发布版剥离。
    /// </summary>
    public static void Tick()
    {
        if (!Enabled || Paused) return; // 6.3 暂停只停采集
        float now = Time.time;
        if (now < _nextSampleTime) return;
        _nextSampleTime = now + SampleInterval;

        _buffer.Clear();
        var trees = BTTreeRegistry.Trees;
        for (int t = 0; t < trees.Count; t++)
        {
            var entry = trees[t];
            if (entry == null || entry.Root == null) continue;
            int start = _buffer.Count;
            Collect(entry.Root, entry.Name, 0, -1);

            // 2.3/2.4 数据源：当前行为名写入所属动物的 Blackboard.CurrentBehavior
            // （取快照中第一个 Running 的顶层分支名；无 Running 则按根状态标"空闲/无行为"）。
            // 仅 Editor 采样时写入（内存字段，不标记场景 dirty），蜜蜂等无 AnimalBase 的树跳过。
            string behavior = FindRunningBranch(start, _buffer.Count);
            if (behavior == null)
                behavior = entry.Root.LastState == BTNode.State.Success ? "空闲" : "无行为";
            if (entry.Owner is Component comp)
            {
                var animal = comp.GetComponent<AnimalBase>();
                if (animal != null)
                    animal.Board.CurrentBehavior = behavior;
            }
        }
        _version++;
    }

    /// <summary>在快照区间 [start, end) 内找第一个 Running 的顶层分支（Depth==1）节点名；找不到返回 null。</summary>
    private static string FindRunningBranch(int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            var s = _buffer[i];
            if (s.Depth == 1 && s.State == BTNode.State.Running)
                return s.NodeName;
        }
        return null;
    }

    private static void Collect(BTNode node, string treeName, int depth, int parentIndex)
    {
        if (node == null) return;
        int index = _buffer.Count;
        _buffer.Add(new NodeSnapshot(treeName, node.NodeName, node.LastState, node.TickCount,
            node.LastTickTime, depth, index, parentIndex));
        var children = node.Children;
        for (int i = 0; i < children.Count; i++)
            Collect(children[i], treeName, depth + 1, index);
    }

    /// <summary>
    /// 强制刷新并打印快照到 Console。treeName 为空 = 打印全部树；否则只打印该树。
    /// 满足阶段 1 验收：Editor 里实时 dump 任意树的节点状态快照。
    /// </summary>
    public static void Dump(string treeName = null)
    {
        Tick(); // 强制按当前采样节奏刷新（未到采样点则沿用缓存）

        var sb = new StringBuilder(512);
        string lastTree = null;
        int printed = 0;
        for (int i = 0; i < _buffer.Count; i++)
        {
            var s = _buffer[i];
            if (treeName != null && s.TreeName != treeName) continue;
            if (treeName == null && s.TreeName != lastTree)
            {
                sb.AppendLine();
                sb.AppendLine($"【树】{s.TreeName} ────────");
                lastTree = s.TreeName;
            }
            sb.Append(' ', s.Depth * 2);
            sb.Append(s.Index).Append("# ").Append(s.NodeName)
              .Append(" -> ").Append(s.State)
              .Append(" tick=").Append(s.TickCount)
              .Append(" @").Append(s.LastTickTime.ToString("F2")).Append('s');
            sb.AppendLine();
            printed++;
        }

        if (printed == 0)
        {
            Debug.Log($"[BTDebugger] 无匹配快照{(treeName != null ? $"（树名='{treeName}'）" : "（注册表为空或未注册树）")}。当前注册树数={BTTreeRegistry.Count}");
            return;
        }
        Debug.Log($"[BTDebugger] ====== 行为树节点快照（{printed} 节点, 版本 v{_version}）======\n{sb}");
    }

    /// <summary>打印全部树的快照。</summary>
    public static void DumpAll() => Dump(null);

    [MenuItem("Tools/BT/打印全部树")]
    private static void MenuDumpAll() => DumpAll();

    [MenuItem("Tools/BT/切换采样")]
    private static void MenuToggleSampling()
    {
        Enabled = !Enabled;
        Debug.Log($"[BTDebugger] 采样已{(Enabled ? "开启" : "关闭")}（间隔 {SampleInterval}s）");
    }

    [MenuItem("Tools/BT/暂停采集")]
    private static void MenuTogglePause()
    {
        Paused = !Paused;
        Debug.Log($"[BTDebugger] 采集已{(Paused ? "暂停" : "恢复")}（仅停采集，不动 timeScale）");
    }

    [MenuItem("Tools/BT/导出时间线")]
    private static void MenuExportTimeline()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "..", "BT_Timeline.txt");
        ExportTimeline(System.IO.Path.GetFullPath(path));
    }

    /// <summary>每帧驱动采集器（隐式挂载，不占场景）。</summary>
    private sealed class Driver : MonoBehaviour
    {
        private void Update() => BTDebugger.Tick();
    }
}
#endif
