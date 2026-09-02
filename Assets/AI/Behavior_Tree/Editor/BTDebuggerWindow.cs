#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [BT] 调试器主窗口（阶段 2.1 + 2.3）：
/// - 2.1 树形面板：左侧按层级展示选中树的所有节点，按 LastState 着色
///       （绿=Success / 红=Failure / 黄=Running），点击节点在右侧看详情。
/// - 2.3 黑板面板：右侧显示选中树所属动物的 Blackboard 关键字段实时值
///       （ThreatLevel/IsBossDetected/CurrentBehavior/…），不打断游戏即可看 AI 认知状态。
/// 数据来自 BTDebugger 快照（0.2s 采样，播放时 EditorApplication.update 自动刷新）。
/// 菜单：Tools/BT/Debugger Window
/// </summary>
public class BTDebuggerWindow : EditorWindow
{
    private int _treeIndex;      // 选中树（BTTreeRegistry.Trees 下标）
    private int _nodeIndex = -1; // 选中节点（快照 Index）
    private Vector2 _treeScroll;
    private Vector2 _panelScroll;
    private string[] _treeNames = System.Array.Empty<string>();

    private static readonly Color ColorSuccess = new Color(0.30f, 0.78f, 0.42f);
    private static readonly Color ColorFailure = new Color(0.82f, 0.36f, 0.36f);
    private static readonly Color ColorRunning = new Color(0.95f, 0.78f, 0.25f);
    private static readonly Color ColorIdle = new Color(0.6f, 0.6f, 0.6f);

    private static GUIStyle _rowStyle;
    private static GUIStyle _rowSelectedStyle;
    private static GUIStyle _monoStyle;

    [MenuItem("Tools/BT/调试窗口")]
    public static void Open()
    {
        GetWindow<BTDebuggerWindow>("BT 调试器");
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // 播放中且有数据变化时自动重绘（窗口关闭时 Repaint 为空操作）
        if (!Application.isPlaying) return;
        if (BTDebugger.Version != _lastVersion)
        {
            _lastVersion = BTDebugger.Version;
            Repaint();
        }
    }

    private int _lastVersion = -1;

    private void OnGUI()
    {
        EnsureStyles();

        var trees = BTTreeRegistry.Trees;

        // ---- 顶部工具条 ----
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        BTDebugger.Enabled = GUILayout.Toggle(BTDebugger.Enabled, "采集", EditorStyles.toolbarButton);
        BTDebugger.Paused = GUILayout.Toggle(BTDebugger.Paused, "暂停", EditorStyles.toolbarButton);
        BTDebugger.SampleInterval = EditorGUILayout.Slider("采样间隔", BTDebugger.SampleInterval, 0.05f, 1f);
        if (GUILayout.Button("立即采样", EditorStyles.toolbarButton))
        {
            BTDebugger.Enabled = true;
            BTDebugger.Tick();
        }
        EditorGUILayout.Space();
        BTDebugger.LogStateChanges = GUILayout.Toggle(BTDebugger.LogStateChanges, "状态日志", EditorStyles.toolbarButton);
        if (GUILayout.Button("导出时间线", EditorStyles.toolbarButton))
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "BT_Timeline.txt");
            BTDebugger.ExportTimeline(System.IO.Path.GetFullPath(path));
        }
        if (!Application.isPlaying)
        {
            GUILayout.Label("（进入 Play 后开始采集）", EditorStyles.toolbarButton);
        }
        EditorGUILayout.EndHorizontal();

        // 树选择下拉
        if (_treeNames.Length != trees.Count)
            RefreshTreeNames(trees);
        if (_treeIndex >= _treeNames.Length)
            _treeIndex = _treeNames.Length - 1;
        if (_treeIndex < 0)
            _treeIndex = 0;

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("树:", EditorStyles.toolbarButton);
        int newIndex = EditorGUILayout.Popup(_treeIndex, _treeNames, GUILayout.MinWidth(200));
        if (newIndex != _treeIndex)
        {
            _treeIndex = newIndex;
            _nodeIndex = -1;
        }
        if (trees.Count > 0)
        {
            var entry = trees[_treeIndex];
            GUILayout.Label($"注册时间 {entry.RegisteredTime.ToString("F2")}s", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        if (trees.Count == 0)
        {
            EditorGUILayout.HelpBox("行为树注册表为空。\n播放模式（或场景已有动物）后，*BT.cs 会自动注册。", MessageType.Info);
            return;
        }

        // ---- 主体：左=树形面板 右=详情+黑板 ----
        EditorGUILayout.BeginHorizontal();

        // 左：树形（2.1）
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(280), GUILayout.ExpandWidth(true));
        GUILayout.Label("节点状态（绿=成功 红=失败 黄=执行中）", EditorStyles.boldLabel);
        _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, GUILayout.ExpandHeight(true));
        DrawTreeRows(trees[_treeIndex]);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 右：详情 + 黑板（2.3）
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(280));
        _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll);
        DrawNodeDetails();
        DrawBlackboard(trees[_treeIndex]);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        // ---- 6.1 状态时间线（底部）----
        DrawTimeline();
    }

    // ===================== 6.1 状态时间线 =====================

    private Vector2 _timelineScroll;

    private void DrawTimeline()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label($"状态时间线（{BTDebugger.TimelineCount}/{BTDebugger.TimelineCapacity} 条，最近 200 条）", EditorStyles.boldLabel);
            if (GUILayout.Button("清空", GUILayout.Width(48)))
                BTDebugger.ClearTimeline();
        }

        var entries = BTDebugger.Timeline;
        int start = Mathf.Max(0, entries.Length - 200);
        _timelineScroll = EditorGUILayout.BeginScrollView(_timelineScroll, GUILayout.ExpandHeight(true));
        for (int i = start; i < entries.Length; i++)
        {
            var e = entries[i];
            Color c = e.State switch
            {
                BTNode.State.Success => ColorSuccess,
                BTNode.State.Failure => ColorFailure,
                _ => ColorRunning,
            };
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(e.Time.ToString("F2") + "s", GUILayout.Width(52));
                GUILayout.Label(e.TreeName, GUILayout.Width(70));
                GUILayout.Label(e.NodeName, GUILayout.ExpandWidth(true));
                GUILayout.Label(e.State.ToString(), new GUIStyle(_monoStyle) { normal = { textColor = c } });
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ===================== 2.1 树形面板 =====================

    private void DrawTreeRows(BTTreeRegistry.Entry entry)
    {
        var snaps = BTDebugger.Snapshots;
        for (int i = 0; i < snaps.Count; i++)
        {
            var s = snaps[i];
            if (s.TreeName != entry.Name) continue;

            bool selected = _nodeIndex == s.Index;
            GUIStyle style = selected ? _rowSelectedStyle : _rowStyle;
            style.padding.left = 8 + s.Depth * 14;
            style.normal.textColor = StateColor(s.State);

            if (GUILayout.Button($"{(s.State == BTNode.State.Running ? "▶ " : "")}{s.NodeName}", style, GUILayout.ExpandWidth(true)))
                _nodeIndex = selected ? -1 : s.Index;
        }
    }

    private void DrawNodeDetails()
    {
        GUILayout.Label("节点详情", EditorStyles.boldLabel);
        var snaps = BTDebugger.Snapshots;
        if (_nodeIndex < 0 || _nodeIndex >= snaps.Count)
        {
            GUILayout.Label("（点击左侧节点查看详情）", EditorStyles.miniLabel);
            EditorGUILayout.Space();
            return;
        }

        var s = snaps[_nodeIndex];
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("节点", s.NodeName, _monoStyle);
            EditorGUILayout.LabelField("状态", s.State.ToString(),
                new GUIStyle(_monoStyle) { normal = { textColor = StateColor(s.State) } });
        }
        EditorGUILayout.LabelField("Tick 次数", s.TickCount.ToString(), _monoStyle);
        EditorGUILayout.LabelField("上次 Tick", s.LastTickTime.ToString("F3") + "s", _monoStyle);
        EditorGUILayout.LabelField("深度", s.Depth.ToString(), _monoStyle);
        EditorGUILayout.LabelField("树", s.TreeName, _monoStyle);
        EditorGUILayout.Space();
    }

    // ===================== 2.3 黑板面板 =====================

    private void DrawBlackboard(BTTreeRegistry.Entry entry)
    {
        GUILayout.Label("黑板（Blackboard）", EditorStyles.boldLabel);

        var comp = entry.Owner as Component;
        var animal = comp != null ? comp.GetComponent<AnimalBase>() : null;
        if (animal == null || animal.Board == null)
        {
            GUILayout.Label("（该树所属对象无 AnimalBase 黑板）", EditorStyles.miniLabel);
            return;
        }

        var bb = animal.Board;
        // 关键认知字段（阶段 2.3 要求：ThreatLevel/IsBossDetected/CurrentBehavior…）
        LabelRow("行为", bb.CurrentBehavior, bb.CurrentBehavior == "空闲" ? ColorIdle : ColorRunning);
        LabelRow("威胁值", bb.ThreatLevel.ToString("F1") + " / 100", Color.Lerp(ColorSuccess, ColorFailure, Mathf.Clamp01(bb.ThreatLevel / 100f)));
        LabelRow("紧迫威胁", BoolText(bb.IsThreatUrgent), ThreatColor(bb.IsThreatUrgent));
        LabelRow("主导威胁", bb.CurrentThreatPriority.ToString());
        LabelRow("检测到BOSS", BoolText(bb.IsBossDetected), ThreatColor(bb.IsBossDetected));
        LabelRow("BOSS距离", bb.BossDistance.ToString("F1"));
        LabelRow("BOSS威胁", bb.BossThreatLevel.ToString("F1"));
        LabelRow("玩家可见", BoolText(bb.IsPlayerVisible), ThreatColor(bb.IsPlayerVisible));
        LabelRow("玩家距离", bb.PlayerDistance.ToString("F1"));
        LabelRow("同形态", BoolText(bb.IsPlayerSameForm));
        LabelRow("眩晕", BoolText(bb.IsStunned), ThreatColor(bb.IsStunned));
        LabelRow("无敌", BoolText(bb.IsInvincible));
        LabelRow("食物", bb.IsFoodDetected ? bb.FoodDistance.ToString("F1") : "—");
        LabelRow("伤害源记忆", BoolText(bb.HasHazardMemory), ThreatColor(bb.HasHazardMemory));
        LabelRow("搜索中", BoolText(bb.ShouldSearch));
    }

    private static void LabelRow(string key, string value, Color? valueColor = null)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(key, GUILayout.Width(88));
            var style = valueColor.HasValue ? new GUIStyle(_monoStyle) { normal = { textColor = valueColor.Value } } : _monoStyle;
            EditorGUILayout.LabelField(value, style);
        }
    }

    private static string BoolText(bool v) => v ? "✓" : "✗";
    private static Color ThreatColor(bool v) => v ? ColorFailure : ColorSuccess;

    // ===================== 工具 =====================

    private static Color StateColor(BTNode.State s)
    {
        return s switch
        {
            BTNode.State.Success => ColorSuccess,
            BTNode.State.Failure => ColorFailure,
            _ => ColorRunning,
        };
    }

    private void RefreshTreeNames(System.Collections.Generic.IReadOnlyList<BTTreeRegistry.Entry> trees)
    {
        var names = new string[trees.Count];
        for (int i = 0; i < trees.Count; i++)
            names[i] = $"{i}: {trees[i].Name}";
        _treeNames = names;
    }

    private static void EnsureStyles()
    {
        if (_rowStyle == null)
        {
            _rowStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                stretchWidth = true,
            };
            _rowStyle.normal.background = null;

            _rowSelectedStyle = new GUIStyle(_rowStyle)
            {
                fontStyle = FontStyle.Bold,
            };
            _rowSelectedStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.9f, 0.25f));

            _monoStyle = new GUIStyle(EditorStyles.label) { font = EditorStyles.standardFont };
        }
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
#endif
