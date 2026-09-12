#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>树形节点图画布（查看模式）：按 BTDebugger 快照重建节点与连线，状态着色。</summary>
public class BTGraphView : GraphView
{
    private readonly Dictionary<int, BTNodeView> _nodeViews = new Dictionary<int, BTNodeView>();

    public BTGraphView()
    {
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();
    }

    /// <summary>清空并依据 BTDebugger 快照重建指定树的节点图（treeName 为空 = 仅清空）。</summary>
    public void Rebuild(string treeName)
    {
        this.DeleteElements(this.graphElements.ToList());
        _nodeViews.Clear();

        if (string.IsNullOrEmpty(treeName))
            return; // 提示条由窗口层负责（_hint），画布只清空

        var snaps = new List<BTDebugger.NodeSnapshot>();
        var all = BTDebugger.Snapshots;
        for (int i = 0; i < all.Count; i++)
            if (all[i].TreeName == treeName)
                snaps.Add(all[i]);
        if (snaps.Count == 0) return;

        foreach (var s in snaps)
        {
            var view = new BTNodeView(s);
            _nodeViews[s.Index] = view;
            AddElement(view);
            // 树形布局：x 按深度分列，y 按遍历序堆叠
            view.SetPosition(new Rect(s.Depth * 170f, s.Index * 62f, 140f, 44f));
        }

        foreach (var s in snaps)
        {
            if (s.ParentIndex < 0) continue;
            if (!_nodeViews.TryGetValue(s.ParentIndex, out var parent)) continue;
            var child = _nodeViews[s.Index];
            var edge = new Edge { output = parent.Output, input = child.Input };
            edge.input.Connect(edge);
            edge.output.Connect(edge);
            AddElement(edge);
        }

        schedule.Execute(() => FrameAll()).ExecuteLater(60);
    }
}

/// <summary>单个节点视图（查看模式）：标题 = 节点名，顶/底端口接父子连线，色块随状态刷新。</summary>
public class BTNodeView : Node
{
    public readonly Port Input;
    public readonly Port Output;

    public BTNodeView(BTDebugger.NodeSnapshot snap)
    {
        title = snap.NodeName;
        tooltip = $"{snap.NodeName}\n状态={snap.State} Tick={snap.TickCount} 最近={snap.LastTickTime:F2}s";

        // 节点标题文字用黑色（GraphView 默认深色主题为白字，黑字在浅色节点上更清晰）
        var titleLabels = titleContainer.Query<Label>().ToList();
        foreach (var label in titleLabels)
            label.style.color = Color.black;

        Input = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
        Input.portName = "";
        inputContainer.Add(Input);

        Output = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
        Output.portName = "";
        outputContainer.Add(Output);

        RefreshState(snap.State);
        RefreshExpandedState();
    }

    public void RefreshState(BTNode.State state)
    {
        var color = state switch
        {
            BTNode.State.Success => new Color(0.30f, 0.78f, 0.42f),
            BTNode.State.Failure => new Color(0.82f, 0.36f, 0.36f),
            _ => new Color(0.95f, 0.78f, 0.25f),
        };
        titleContainer.style.backgroundColor = color;
    }
}
#endif
