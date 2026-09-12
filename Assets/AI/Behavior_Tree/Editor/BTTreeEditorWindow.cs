#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// [BT] 行为树编辑器（独立专用编辑窗口）：
/// 从零/从 JSON 可视化拖拽编辑行为树——右键添加节点、Port 连线（含约束校验）、
/// 删除子树、底部参数面板（显示名 + params）、保存导出 Resources/BTTrees/*.json。
/// 编辑数据 = BTEditorNode 模型，与 BTLayoutParser/Exporter 双向无损（E0/E1/E2）。
/// 菜单：Tools/BT/树编辑器
/// </summary>
public class BTTreeEditorWindow : EditorWindow
{
    private const string TreesPath = "BTTrees";

    private BTEditableGraphView _editView;   // 编辑画布
    private DropdownField _treeDropdown;     // 树下拉（打开/切换）
    private Button _saveButton;
    private Label _dirtyLabel;               // 未保存指示（P0 5.1）
    private Label _bindingLabel;             // 树↔动物绑定指示（P1 需求②）
    private IMGUIContainer _inspector;       // 底部参数面板（E1.4）

    private TextAsset _openAsset;            // 当前打开的 JSON 资产（null = 新建未落盘）
    private string _editTreeName = "";       // 当前树名
    private string _bindingRefreshTree = ""; // 绑定标签刷新缓存
    private List<TextAsset> _treeAssets = new List<TextAsset>();
    private int _lastNodeCount = -1;         // 外部结构变化检测基线
    private int _lastEdgeCount = -1;

    [MenuItem("Tools/BT/树编辑器")]
    public static void Open()
    {
        GetWindow<BTTreeEditorWindow>("行为树编辑器");
    }

    private void OnEnable()
    {
        RefreshAssetCache();

        // 顶部工具条（不用 Toolbar 类：2022.3 中访问级别受限）
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.paddingTop = 2f;
        toolbar.style.paddingBottom = 2f;

        _treeDropdown = new DropdownField("树: ");
        _treeDropdown.style.minWidth = 160f;
        _treeDropdown.RegisterValueChangedCallback(evt =>
        {
            if (!ConfirmDiscardChanges())
            {
                _treeDropdown.SetValueWithoutNotify(_editTreeName); // 取消切换
                return;
            }
            var asset = _treeAssets.FirstOrDefault(a => a.name == evt.newValue);
            OpenTree(asset);
        });
        toolbar.Add(_treeDropdown);

        // 新建树（P0 3.1）：输入树名 → 空白画布 + 置脏，保存时写新文件
        toolbar.Add(new Button(NewTree) { text = "新建" });

        // 清空画布（P0 6.2）
        toolbar.Add(new Button(ClearCanvas) { text = "清空" });

        // 适应视图：手动 FrameAll（不依赖操纵器 API，跨版本稳定）
        toolbar.Add(new Button(() => _editView?.FrameAll()) { text = "适应视图" });

        // 自动排列：一键整理节点（含游离根排到右侧，杜绝叠堆）
        toolbar.Add(new Button(() => _editView?.AutoLayout()) { text = "自动排列" });

        // 另存为：当前树深拷贝为新树（模板复用）
        toolbar.Add(new Button(SaveAsNewTree) { text = "另存为" });

        _saveButton = new Button(SaveCurrentTree) { text = "保存" };
        toolbar.Add(_saveButton);

        _dirtyLabel = new Label("");
        _dirtyLabel.style.marginLeft = 8f;
        _dirtyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toolbar.Add(_dirtyLabel);

        _bindingLabel = new Label("");
        _bindingLabel.style.marginLeft = 8f;
        toolbar.Add(_bindingLabel);

        toolbar.style.flexShrink = 0f;
        rootVisualElement.Add(toolbar);

        // 编辑画布：占满剩余空间（flex 布局而非 absolute Stretch，避免盖住工具条/面板）
        _editView = new BTEditableGraphView();
        _editView.style.flexGrow = 1f;
        _editView.style.minHeight = 120f;
        rootVisualElement.Add(_editView);

        // 底部参数面板（IMGUIContainer 与 UIElements 共存）
        _inspector = new IMGUIContainer(DrawInspector);
        _inspector.style.maxHeight = 320f;
        _inspector.style.flexShrink = 0f;
        _inspector.style.paddingTop = 4f;
        rootVisualElement.Add(_inspector);

        RefreshDropdownChoices();
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // 检测用户拖线/断线等外部结构变化（图内创建/删除已由 BTEditableGraphView 标记）
        int nc = _editView.NodeCount;
        int ec = _editView.EdgeCount;
        if (nc != _lastNodeCount || ec != _lastEdgeCount)
        {
            _lastNodeCount = nc;
            _lastEdgeCount = ec;
            _editView.MarkExternalChange();
        }

        // 未保存指示
        if (_dirtyLabel != null)
        {
            _dirtyLabel.text = _editView.IsDirty ? "● 未保存" : "";
            _dirtyLabel.style.color = _editView.IsDirty ? Color.red : Color.clear;
        }

        // 树↔动物绑定指示（树名变化才查询）
        if (_bindingLabel != null && _bindingRefreshTree != _editTreeName)
        {
            _bindingRefreshTree = _editTreeName;
            var loaders = BTBindingResolver.LoaderTypesForTree(_editTreeName);
            if (string.IsNullOrEmpty(_editTreeName))
            {
                _bindingLabel.text = "";
            }
            else if (loaders.Count > 0)
            {
                _bindingLabel.text = $"绑定: {loaders[0]}";
                _bindingLabel.style.color = new Color(0.25f, 0.55f, 0.9f);
            }
            else
            {
                _bindingLabel.text = "未绑定: 无组件加载此树";
                _bindingLabel.style.color = Color.yellow;
            }
        }

        // 参数面板每帧重绘（响应 TextField 输入）
        _inspector?.MarkDirtyRepaint();
    }

    // ==================== 打开 / 保存 ====================

    private void RefreshAssetCache()
    {
        _treeAssets = Resources.LoadAll<TextAsset>(TreesPath).ToList();
    }

    private void RefreshDropdownChoices()
    {
        var names = _treeAssets.Select(a => a.name).ToList();
        _treeDropdown.choices = names;
        if (names.Count == 0)
        {
            _editTreeName = "";
            _treeDropdown.SetValueWithoutNotify("");
            OpenTree(null);
            return;
        }
        if (!names.Contains(_editTreeName))
            _editTreeName = names[0];
        _treeDropdown.SetValueWithoutNotify(_editTreeName);
        OpenTree(_treeAssets.FirstOrDefault(a => a.name == _editTreeName));
    }

    private void OpenTree(TextAsset asset)
    {
        _openAsset = asset;
        _editTreeName = asset != null ? asset.name : "";
        if (_editView == null) return;
        _editView.Load(asset != null ? BTLayoutParser.ParseLayout(asset.text) : null);
        ResetDirtyBaseline();
    }

    private void ResetDirtyBaseline()
    {
        _lastNodeCount = _editView != null ? _editView.NodeCount : 0;
        _lastEdgeCount = _editView != null ? _editView.EdgeCount : 0;
    }

    /// <summary>保存当前编辑树到 Resources/BTTrees/*.json（校验见 BTEditableGraphView.Save）。</summary>
    private void SaveCurrentTree()
    {
        if (_editView == null) return;
        var model = _editView.Save(); // 内部含保存前校验 + 成功后清脏
        if (model == null) return;

        string json = BTLayoutExporter.Export(model);
        string path = _openAsset != null
            ? AssetDatabase.GetAssetPath(_openAsset)
            : $"Assets/Resources/{TreesPath}/{_editTreeName}.json";
        File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[BTEditor] 已保存树到 {path}");
        RefreshAssetCache();
        RefreshDropdownChoices();
        ResetDirtyBaseline();
    }

    /// <summary>另存为：把当前树深拷贝为新树（模板复用起点），原名文件不受影响。</summary>
    private void SaveAsNewTree()
    {
        if (_editView == null) return;
        var model = _editView.Save(); // 含校验；校验失败（弹窗）则中止
        if (model == null) return;
        var copy = model.Clone();

        InputDialog.Show("另存为新树", _editTreeName + "2", name =>
        {
            string treeName = string.IsNullOrWhiteSpace(name) ? (_editTreeName + "2") : name.Trim();
            _openAsset = null;
            _editTreeName = treeName;
            _editView.Load(copy);      // 渲染副本
            _editView.MarkDirty();     // 新文件未落盘 = 未保存
            ResetDirtyBaseline();
            _treeDropdown.SetValueWithoutNotify(treeName);
            SaveCurrentTree();         // 立即落盘新文件（Save 内部刷新资产/下拉并清脏）
            if (BTBindingResolver.IsTreeBound(treeName))
                Debug.Log($"[BTEditor] 已另存为「{treeName}」，运行时由 {BTBindingResolver.LoaderTypesForTree(treeName)[0]} 加载");
        });
    }

    // ==================== 新建树 / 清空 / 保存确认 ====================

    private void NewTree()
    {
        if (!ConfirmDiscardChanges()) return;
        InputDialog.Show("新建行为树", "NewTree", name =>
        {
            string treeName = string.IsNullOrWhiteSpace(name) ? "NewTree" : name.Trim();
            _openAsset = null;
            _editTreeName = treeName;
            _editView.Load(null);          // 清空画布（Load 置干净）
            _editView.MarkDirty();         // 新建即未保存
            ResetDirtyBaseline();
            _treeDropdown.SetValueWithoutNotify(treeName);
            _editView.FrameAll();
            // 绑定引导（P1 需求②）：命名约定，TreeAssetName 决定谁加载这棵树
            if (BTBindingResolver.IsTreeBound(treeName))
            {
                EditorUtility.DisplayDialog("新建树", $"空白画布已就绪，树名「{treeName}」已被 {BTBindingResolver.LoaderTypesForTree(treeName)[0]} 加载（见工具条\"绑定\"）。\n右键添加第一个节点作为根，编辑完后点\"保存\"生成 JSON。", "知道了");
            }
            else
            {
                EditorUtility.DisplayDialog("新建树", $"空白画布已就绪。\n注意：「{treeName}」当前没有任何组件加载它（工具条显示\"未绑定\"）。\n要让某只动物使用它：把该动物的 *BT 组件里 TreeAssetName 改成「{treeName}」。\n右键添加第一个节点作为根，编辑完后点\"保存\"生成 JSON。", "知道了");
            }
        });
    }

    private void ClearCanvas()
    {
        if (_editView == null) return;
        if (_editView.IsDirty && !EditorUtility.DisplayDialog("清空画布", "当前树有未保存的更改，清空后无法恢复。确定继续？", "清空", "取消"))
            return;
        if (!EditorUtility.DisplayDialog("清空画布", "确定清空当前画布？此操作不可撤销（文件不受影响，直到再次保存）。", "清空", "取消"))
            return;
        _editView.Load(null);
        _editView.MarkDirty();
        ResetDirtyBaseline();
    }

    /// <summary>切换树前确认未保存改动。返回 false = 用户取消切换。</summary>
    private bool ConfirmDiscardChanges()
    {
        if (_editView == null || !_editView.IsDirty) return true;
        int choice = EditorUtility.DisplayDialogComplex("未保存的更改",
            $"树 '{_editTreeName}' 有未保存的更改，要保存吗？", "保存", "不保存", "取消");
        if (choice == 0) SaveCurrentTree(); // 保存成功后内部已清脏
        if (choice == 2) return false;      // 取消
        return true;
    }

    // ==================== 参数面板（E1.4） ====================

    private void DrawInspector()
    {
        if (_editView == null) return;

        var view = _editView.selection.OfType<EditNodeView>().FirstOrDefault();
        if (view == null)
        {
            // 未选中：树概要 + 节点颜色图例 + 操作提示（全局面板）
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("未选中节点：点击画布中的节点，在下方编辑显示名与参数。");
            GUILayout.Space(4);

            string treeLabel = string.IsNullOrEmpty(_editTreeName) ? "(新建/未命名)" : _editTreeName;
            GUILayout.Label($"当前树：{treeLabel}");
            var loaders = BTBindingResolver.LoaderTypesForTree(_editTreeName);
            if (loaders.Count > 0)
                GUILayout.Label($"绑定：{loaders[0]}（运行时加载此树）");
            else if (!string.IsNullOrEmpty(_editTreeName))
                GUILayout.Label("绑定：未绑定（无组件加载此树）");
            GUILayout.Label($"节点数：{_editView.NodeCount}　连线数：{_editView.EdgeCount}");

            GUILayout.Space(6);
            GUILayout.Label("节点颜色图例（标题背景）：");
            foreach (var cObj in Enum.GetValues(typeof(BTNodeCategory)))
            {
                var category = (BTNodeCategory)cObj;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16);
                Color prev = GUI.color;
                GUI.color = BTEditorStyle.ColorFor(category);
                GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(14));
                GUI.color = prev;
                GUILayout.Label(BTEditorStyle.DisplayName(category) + "节点");
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            GUILayout.Label("操作：空白处右键=添加节点｜拖端口=连线｜中键拖拽=平移｜滚轮=缩放");
            EditorGUILayout.EndVertical();
            return;
        }

        var model = view.Model;
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("节点类型", model.type);

        string newName = EditorGUILayout.TextField("显示名", model.name ?? "");
        if (newName != model.name)
        {
            model.name = newName;
            view.RefreshName();
            _editView.MarkDirty();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("参数 (params)");

        if (model.@params != null)
        {
            for (int i = model.@params.Count - 1; i >= 0; i--)
            {
                var p = model.@params[i];
                EditorGUILayout.BeginHorizontal();
                string k = EditorGUILayout.TextField(p.key ?? "");
                string v = EditorGUILayout.TextField(p.value ?? "");
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    model.@params.RemoveAt(i);
                    _editView.MarkDirty();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                if (k != p.key)
                {
                    p.key = k;
                    _editView.MarkDirty();
                }
                if (v != p.value)
                {
                    p.value = v;
                    _editView.MarkDirty();
                }
            }
        }

        if (GUILayout.Button("+ 添加参数"))
        {
            if (model.@params == null) model.@params = new List<BTEditorParam>();
            model.@params.Add(new BTEditorParam("key", "value"));
            _editView.MarkDirty();
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
