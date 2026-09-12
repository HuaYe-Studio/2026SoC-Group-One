#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// [BT] 可编辑节点图（阶段 E1）：GraphView 拖拽编辑行为树。
/// - E1.2 节点创建：空白处右键 → 分组添加节点菜单（组合/装饰/注册/叶子/子树，BTNodeCatalog 数据源）；
/// - E1.3 连线编辑：横版 Port 拖线连接/断开；单父/装饰器单子由 Port Capacity（Input Single / 装饰器 Output Single）原生约束；
/// - E1.5 删除：节点右键删除（含子树，带确认）、添加子节点；显示名/参数在窗口底部面板编辑。
///
/// 兼容性（重要）：本类不依赖任何新版 GraphView API（viewTransform/manipulators/AddManipulator/
/// InstantiatePort/GraphViewChange.edgesToRemove 均不用），只使用 2019 起稳定的 API：
/// - Port.Create&lt;Edge&gt;(...) 建端口；
/// - 父子关系不用 graphViewChanged 实时维护，改为在需要时 RebuildRelationships()（遍历 this.edges）；
/// - 右键菜单：ContextualMenuPopulateEvent + 路径式 AppendAction（"分组/节点名" 自动成子菜单）。
/// 校验集中在 Save()：无根/多根、环、装饰器单子。
/// </summary>
public class BTEditableGraphView : GraphView
{
    public const float NodeWidth = 170f;
    public const float NodeHeight = 70f;
    private const float HGap = 20f;
    private const float VGap = 40f;

    /// <summary>是否有未保存的结构改动（P0 脏标记）：创建/删除/连线变化/参数编辑后为 true，Save 成功后为 false。</summary>
    public bool IsDirty { get; private set; }

    /// <summary>当前图中节点数（窗口层用于检测用户拖线等外部结构变化）。</summary>
    public int NodeCount => graphElements.OfType<EditNodeView>().Count();

    /// <summary>当前图中连线数。</summary>
    public int EdgeCount
    {
        get
        {
            int count = 0;
            foreach (var _ in edges) count++;
            return count;
        }
    }

    /// <summary>标记结构已修改（本图内操作：创建/删除/自动连线）。</summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>标记结构已修改（窗口层发现用户拖线/断线等外部变化后调用）。</summary>
    public void MarkExternalChange() => IsDirty = true;

    public BTEditableGraphView()
    {
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        // 迷你地图（右上角缩略导航）
        var miniMap = new MiniMap { anchored = true };
        miniMap.style.width = 160f;
        miniMap.style.height = 120f;
        miniMap.SetPosition(new Rect(8f, 8f, 160f, 120f));
        Add(miniMap);

        // 空白右键：添加节点（鼠标位置换算为内容坐标系：GraphView 本地 → 内容，兼容无 LocalToWorld/WorldToLocal 的版本）
        RegisterCallback<ContextualMenuPopulateEvent>(evt =>
        {
            evt.menu.ClearItems();
            var c = contentViewContainer;
            Vector3 tp = c.transform.position;
            Vector3 sc = c.transform.scale;
            Vector2 contentPos = new Vector2(
                (evt.mousePosition.x - tp.x) / (Mathf.Abs(sc.x) < 1e-4f ? 1f : sc.x),
                (evt.mousePosition.y - tp.y) / (Mathf.Abs(sc.y) < 1e-4f ? 1f : sc.y));
            BuildAddNodeMenu(evt.menu, contentPos, null);
        });

        // 滚轮缩放（以鼠标为锚点，不用任何新版变换 API：改 contentViewContainer.transform 核心属性）
        RegisterCallback<WheelEvent>(evt =>
        {
            float factor = evt.delta.y > 0f ? 1.1f : 0.9f; // 滚上放大 / 滚下缩小
            ZoomAt(evt.mousePosition, factor);
            evt.StopPropagation();
        });

        // 中键拖拽平移
        RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button == (int)MouseButton.MiddleMouse)
            {
                _panning = true;
                evt.StopPropagation();
            }
        });
        RegisterCallback<MouseMoveEvent>(evt =>
        {
            if (!_panning) return;
            var pos = contentViewContainer.transform.position;
            pos.x += evt.mouseDelta.x;
            pos.y += evt.mouseDelta.y;
            contentViewContainer.transform.position = pos;
            evt.StopPropagation();
        });
        RegisterCallback<MouseUpEvent>(evt =>
        {
            if (evt.button == (int)MouseButton.MiddleMouse) _panning = false;
        });
        RegisterCallback<MouseLeaveEvent>(_ => _panning = false);

        // Del 键删除选中节点（含子树）
        RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Delete && this.selection.Count > 0)
            {
                var views = this.selection.OfType<EditNodeView>().ToList();
                foreach (var v in views) DeleteNode(v, false);
                evt.StopPropagation();
            }
        });
    }

    private bool _panning;

    /// <summary>以视口坐标 p（相对本画布左上）为锚点缩放内容：保持锚点下的节点不动。</summary>
    private void ZoomAt(Vector2 p, float factor)
    {
        var c = contentViewContainer;
        Vector3 s = c.transform.scale;
        float oldX = Mathf.Abs(s.x) < 1e-4f ? 1f : s.x;
        float nx = Mathf.Clamp(oldX * factor, 0.3f, 3f);
        if (Mathf.Abs(nx - oldX) < 0.001f) return;
        Vector3 pos = c.transform.position;
        float mx = (p.x - pos.x) / oldX; // 鼠标指向的内容坐标
        float my = (p.y - pos.y) / oldX;
        c.transform.scale = new Vector3(nx, nx, 1f);
        c.transform.position = new Vector3(p.x - mx * nx, p.y - my * nx, 0f);
    }

    // ==================== 打开 / 保存 ====================

    /// <summary>从模型加载整棵树（清空现有图后重建 + 树形布局 + 居中）。</summary>
    public void Load(BTEditorNode root)
    {
        DeleteElements(graphElements.ToList());
        if (root == null) return;

        var rootView = CreateViewFor(root);
        AddElement(rootView);
        BuildRecursive(rootView, root);
        RebuildRelationships();
        LayoutTree(rootView, 20f, 20f);
        IsDirty = false; // 打开/加载 = 干净起点
        FrameAll();
    }

    private void BuildRecursive(EditNodeView parentView, BTEditorNode parentModel)
    {
        if (parentModel.children == null) return;
        foreach (var childModel in parentModel.children)
        {
            var childView = CreateViewFor(childModel);
            AddElement(childView);
            AddElement(parentView.OutputPort.ConnectTo(childView.InputPort));
            BuildRecursive(childView, childModel);
        }
    }

    /// <summary>图 → 模型（含校验）。校验失败返回 null 并弹窗说明原因。</summary>
    public BTEditorNode Save()
    {
        RebuildRelationships();

        // 1) 找全部根（无父节点）
        var roots = new List<EditNodeView>();
        foreach (var v in AllViews())
            if (v.Parent == null)
                roots.Add(v);
        if (roots.Count == 0)
        {
            EditorUtility.DisplayDialog("无法保存", "图中没有节点，无法保存。\n（空白处右键可添加第一个节点作为根）", "知道了");
            return null;
        }
        if (roots.Count > 1)
        {
            EditorUtility.DisplayDialog("无法保存", $"存在 {roots.Count} 个根节点（未连接的分支）。\n请用输出端口把分支连到主树上，只保留一个根。", "知道了");
            return null;
        }

        // 2) 环检测（DFS 三色法，从根出发）
        var visiting = new HashSet<EditNodeView>();
        var done = new HashSet<EditNodeView>();
        if (HasCycle(roots[0], visiting, done))
        {
            EditorUtility.DisplayDialog("无法保存", "检测到循环连线（环）。\n行为树不允许环，请断开成环的连接。", "知道了");
            return null;
        }

        // 3) 装饰节点必须恰好 1 子
        foreach (var v in AllViews())
        {
            var cat = BTNodeCatalog.Find(v.Model.type);
            if (cat.HasValue && cat.Value.Category == BTNodeCategory.Decorator && v.Children.Count != 1)
            {
                EditorUtility.DisplayDialog("无法保存", $"装饰节点 '{v.Model.name}'（{v.Model.type}）必须有且仅有 1 个子节点，当前 {v.Children.Count} 个。", "知道了");
                return null;
            }
        }

        IsDirty = false; // 保存成功 = 干净
        return BuildModel(roots[0]);
    }

    private static bool HasCycle(EditNodeView v, HashSet<EditNodeView> visiting, HashSet<EditNodeView> done)
    {
        if (done.Contains(v)) return false;
        if (visiting.Contains(v)) return true;
        visiting.Add(v);
        foreach (var c in v.Children)
            if (HasCycle(c, visiting, done))
                return true;
        visiting.Remove(v);
        done.Add(v);
        return false;
    }

    private BTEditorNode BuildModel(EditNodeView view)
    {
        var m = view.Model;
        if (view.Children.Count == 0)
        {
            m.children = null;
            return m;
        }
        if (m.children == null) m.children = new List<BTEditorNode>();
        m.children.Clear();
        foreach (var c in view.Children) m.children.Add(BuildModel(c));
        return m;
    }

    // ==================== 节点创建 / 删除 ====================

    /// <summary>空白处右键创建节点（不连线；保留鼠标位置，不自动重排，避免游离节点叠堆）。</summary>
    public void CreateNodeAt(Vector2 contentPos, string type)
    {
        var view = CreateViewFor(new BTEditorNode(type));
        view.SetPosition(new Rect(contentPos, new Vector2(NodeWidth, NodeHeight)));
        AddElement(view);
    }

    /// <summary>为指定节点添加子节点（自动连线 + 重排主树）。</summary>
    public void AddChildNode(EditNodeView parent, string childType)
    {
        if (parent == null || parent.OutputPort == null) return;
        var childView = CreateViewFor(new BTEditorNode(childType));
        AddElement(childView);
        parent.OutputPort.ConnectTo(childView.InputPort);
        AutoLayout();
    }

    /// <summary>删除节点及其整棵子树（confirm=true 时弹确认框）。</summary>
    public void DeleteNode(EditNodeView view, bool confirm)
    {
        if (view == null) return;
        if (confirm && !EditorUtility.DisplayDialog("删除节点", $"确定删除 '{view.Model.name ?? view.Model.type}' 及其所有子节点？", "删除", "取消"))
            return;

        var toDelete = new List<EditNodeView>();
        CollectSubtree(view, toDelete);

        foreach (var v in toDelete)
        {
            DisconnectEdges(v);
            RemoveElement(v);
        }

        LayoutAll();
        MarkDirty();
    }

    /// <summary>选中节点（供右键菜单"在参数面板编辑"使用）。</summary>
    public void SelectNode(EditNodeView view)
    {
        ClearSelection();
        if (view != null) AddToSelection(view);
    }

    // ==================== 端口 ====================

    private void AddPorts(EditNodeView view)
    {
        view.InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
        view.InputPort.portName = "";
        view.inputContainer.Add(view.InputPort);

        var cat = BTNodeCatalog.Find(view.Model.type);
        bool canHaveChildren = cat.HasValue &&
            (cat.Value.Category == BTNodeCategory.Composite || cat.Value.Category == BTNodeCategory.Decorator);
        if (canHaveChildren)
        {
            bool singleChild = cat.Value.Category == BTNodeCategory.Decorator;
            view.OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output,
                singleChild ? Port.Capacity.Single : Port.Capacity.Multi, typeof(bool));
            view.OutputPort.portName = "";
            view.outputContainer.Add(view.OutputPort);
        }
    }

    // ==================== 父子关系重建（不依赖 graphViewChanged） ====================

    /// <summary>遍历图中所有边重建 Parent/Children（跨版本稳定，不依赖 GraphViewChange.edgesToRemove）。</summary>
    public void RebuildRelationships()
    {
        foreach (var v in AllViews())
        {
            v.Parent = null;
            v.Children.Clear();
        }
        foreach (var e in edges)
        {
            var from = e.output?.node as EditNodeView;
            var to = e.input?.node as EditNodeView;
            if (from == null || to == null || from == to) continue;
            to.Parent = from;
            from.Children.Add(to);
        }
    }

    // ==================== 右键菜单（E1.2 / E1.5） ====================

    /// <summary>构造"添加节点"菜单（路径式 AppendAction 自动生成子菜单，避开 AppendSubMenu 版本差异）。
    /// prefix 非空时作为子菜单路径前缀（如"添加子节点/"）。</summary>
    public void BuildAddNodeMenu(UnityEngine.UIElements.DropdownMenu menu, Vector2 screenPos, EditNodeView asChildOf, string prefix = "")
    {
        foreach (var entry in BTNodeCatalog.Entries)
        {
            string groupName = entry.Category switch
            {
                BTNodeCategory.Composite => "组合节点",
                BTNodeCategory.Decorator => "装饰节点",
                BTNodeCategory.Registered => "注册节点",
                BTNodeCategory.Leaf => "逻辑叶子",
                _ => "子树",
            };
            string display = entry.Name.Contains("/") ? entry.Name.Replace("/", "／") : entry.Name;
            menu.AppendAction($"{prefix}{groupName}/{display}", _ =>
            {
                if (asChildOf != null) AddChildNode(asChildOf, entry.Name);
                else CreateNodeAt(screenPos, entry.Name);
            });
        }
    }

    // ==================== 布局 / 工具 ====================

    /// <summary>自动排列全部节点：主根树从 (20,20) 平铺；游离根（未连线分支）依次排到右侧，杜绝叠堆。</summary>
    public void AutoLayout()
    {
        RebuildRelationships();
        var roots = new List<EditNodeView>();
        foreach (var v in AllViews())
            if (v.Parent == null)
                roots.Add(v);
        if (roots.Count == 0) return;

        float x = 20f;
        foreach (var r in roots)
            x += LayoutTree(r, x, 20f);
    }

    private void LayoutAll() => AutoLayout();

    private float LayoutTree(EditNodeView node, float x, float y)
    {
        node.SetPosition(new Rect(x, y, NodeWidth, NodeHeight));
        if (node.Children.Count == 0) return NodeWidth + HGap;

        float cursorX = x;
        float childY = y + NodeHeight + VGap;
        float total = 0f;
        foreach (var c in node.Children)
        {
            float w = LayoutTree(c, cursorX, childY);
            cursorX += w;
            total += w;
        }
        node.SetPosition(new Rect(x + (total - HGap - NodeWidth) / 2f, y, NodeWidth, NodeHeight));
        return total;
    }

    private IEnumerable<EditNodeView> AllViews() => graphElements.OfType<EditNodeView>();

    private static void CollectSubtree(EditNodeView root, List<EditNodeView> result)
    {
        result.Add(root);
        foreach (var c in root.Children.ToList())
            CollectSubtree(c, result);
    }

    private void DisconnectEdges(EditNodeView v)
    {
        if (v.InputPort != null)
            foreach (var e in v.InputPort.connections.ToList())
            {
                e.input.Disconnect(e);
                e.output?.Disconnect(e);
                RemoveElement(e);
            }
        if (v.OutputPort != null)
            foreach (var e in v.OutputPort.connections.ToList())
            {
                e.input?.Disconnect(e);
                e.output.Disconnect(e);
                RemoveElement(e);
            }
    }

    private EditNodeView CreateViewFor(BTEditorNode model)
    {
        var view = new EditNodeView(model, this);
        AddPorts(view);
        return view;
    }
}

/// <summary>编辑模式的节点视图：横版端口 + 标题黑字 + 节点右键菜单。</summary>
public class EditNodeView : Node
{
    public readonly BTEditorNode Model;
    public EditNodeView Parent;
    public readonly List<EditNodeView> Children = new();
    public Port InputPort;
    public Port OutputPort;

    private readonly BTEditableGraphView _graph;

    public EditNodeView(BTEditorNode model, BTEditableGraphView graph)
    {
        Model = model;
        _graph = graph;

        RefreshName();
        tooltip = model.type;

        var titleLabels = titleContainer.Query<Label>().ToList();
        foreach (var label in titleLabels)
            label.style.color = Color.black;

        // 节点右键菜单（StopPropagation 阻止冒泡到画布菜单）
        RegisterCallback<ContextualMenuPopulateEvent>(evt =>
        {
            evt.menu.ClearItems();
            evt.menu.AppendAction("在参数面板编辑", _ => _graph.SelectNode(this));
            evt.menu.AppendSeparator();
            if (OutputPort != null)
            {
                // 直接复用目录菜单，用"添加子节点/"路径前缀生成子菜单（避开 AppendSubMenu）
                _graph.BuildAddNodeMenu(evt.menu, Vector2.zero, this, "添加子节点/");
                evt.menu.AppendSeparator();
            }
            evt.menu.AppendAction("删除子树", _ => _graph.DeleteNode(this, true));
            evt.StopPropagation();
        });

        RefreshExpandedState();
    }

    /// <summary>显示名：优先 Model.name，缺省回退 type。参数面板改名后调用。</summary>
    public void RefreshName()
    {
        title = string.IsNullOrEmpty(Model.name) ? Model.type : Model.name;
        tooltip = Model.type;
    }
}
#endif
