#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// [BT] 节点图窗口（查看专用）：
/// 从 BTDebugger 快照实时渲染运行树结构 + 节点状态颜色（绿=Success 红=Failure 黄=Running）。
/// 编辑树请用 Tools/BT/树编辑器（BTTreeEditorWindow）。
/// 菜单：Tools/BT/节点图
/// </summary>
public class BTGraphWindow : EditorWindow
{
    private BTGraphView _graphView;
    private DropdownField _treeDropdown;
    private Label _hint;

    private int _lastVersion = -1;
    private string _selectedTree = "";

    [MenuItem("Tools/BT/节点图")]
    public static void Open()
    {
        GetWindow<BTGraphWindow>("BT 节点图");
    }

    private void OnEnable()
    {
        // 顶部工具条（不用 Toolbar 类：2022.3 中访问级别受限）
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.paddingTop = 2f;
        toolbar.style.paddingBottom = 2f;

        _treeDropdown = new DropdownField("树: ");
        _treeDropdown.style.minWidth = 160f;
        _treeDropdown.RegisterValueChangedCallback(evt =>
        {
            _selectedTree = evt.newValue;
            _lastVersion = -1; // 强制重建
        });
        toolbar.Add(_treeDropdown);

        // 适应视图：手动触发 FrameAll 居中（不依赖任何操纵器 API，跨版本稳定）
        toolbar.Add(new Button(() => _graphView?.FrameAll()) { text = "适应视图" });
        toolbar.style.flexShrink = 0f;
        rootVisualElement.Add(toolbar);

        // 非播放/未选树时的提示
        _hint = new Label("进入 Play 后选择一棵树，即可查看实时节点状态（节点颜色：绿=成功 红=失败 黄=执行中）。\n编辑/保存树请使用 Tools/BT/树编辑器。");
        _hint.style.color = new Color(0.6f, 0.6f, 0.6f);
        _hint.style.paddingTop = 4f;
        _hint.style.paddingBottom = 4f;
        _hint.style.flexShrink = 0f;
        rootVisualElement.Add(_hint);

        _graphView = new BTGraphView();
        _graphView.style.flexGrow = 1f;
        _graphView.style.minHeight = 120f;
        rootVisualElement.Add(_graphView);

        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (_graphView == null) return;

        // 注册表增删时刷新下拉选项
        var trees = BTTreeRegistry.Trees;
        var names = new List<string>(trees.Count);
        for (int i = 0; i < trees.Count; i++)
            names.Add(trees[i].Name);
        if (!names.SequenceEqual(_treeDropdown.choices))
        {
            _treeDropdown.choices = names;
            if (names.Count > 0)
            {
                if (!names.Contains(_selectedTree))
                    _selectedTree = names[0];
                _treeDropdown.SetValueWithoutNotify(_selectedTree);
            }
        }

        if (!Application.isPlaying)
        {
            _graphView.Rebuild(""); // 清空节点图
            _lastVersion = -1;
        }

        if (_hint != null)
            _hint.style.display = (!Application.isPlaying || string.IsNullOrEmpty(_selectedTree))
                ? DisplayStyle.Flex
                : DisplayStyle.None;

        if (Application.isPlaying && _lastVersion != BTDebugger.Version)
        {
            _lastVersion = BTDebugger.Version;
            _graphView.Rebuild(_selectedTree);
        }
    }
}
#endif
