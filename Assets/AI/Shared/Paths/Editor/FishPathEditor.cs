using UnityEngine;
using UnityEditor;

/// <summary>
/// FishPath 场景绘制工具：在 Scene 视图中可视化并编辑路径点。
/// 交互方式：
/// - 点选路径点：左键点击任意路径点选中它（高亮黄色）
/// - 拖动路径点：按住左键拖动选中的点调整位置
/// - 添加路径点：Shift + 左键点击 Scene 空白处，在点击位置追加一个新点
/// - 删除路径点：选中一个点后按 Delete 键删除
/// - 段颜色：绿色=上浮段，红色=下沉段，青色=前行段（由走向自动判定）
/// </summary>
[CustomEditor(typeof(FishPath))]
public class FishPathEditor : Editor
{
    private const float HandleSize = 0.15f;
    private FishPath _path;
    private int _selectedIndex = -1;
    private bool _dragging;

    private void OnEnable()
    {
        _path = (FishPath)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("路径点数: " + (_path.Points?.Count ?? 0), EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("追加一个点"))
            AddPointAtEnd();
        if (GUILayout.Button("清空路径"))
        {
            _path.Points.Clear();
            _selectedIndex = -1;
            EditorUtility.SetDirty(_path);
        }
        EditorGUILayout.EndHorizontal();

        if (_path.Points.Count < 2)
            EditorGUILayout.HelpBox("路径至少需要 2 个点才能形成游动路线。", MessageType.Info);

        if (GUILayout.Button("把路径点烘焙为鱼的世界坐标"))
        {
            // 便捷工具：将路径整体平移到选中物体所在位置（便于从原点开始画）
            Vector2 offset = _path.transform.position;
            for (int i = 0; i < _path.Points.Count; i++)
                _path.Points[i] += offset;
            EditorUtility.SetDirty(_path);
        }

        if (GUI.changed)
            EditorUtility.SetDirty(_path);
    }

    private void OnSceneGUI()
    {
        if (_path.Points == null || _path.Points.Count == 0)
            return;

        // 1. 绘制样条曲线（按走向着色，与运行时一致的曲线路径）
        const int SamplesPerSegment = 12;
        for (int i = 0; i < _path.SegmentCount; i++)
        {
            Handles.color = SegmentColor(i);
            Vector3 prev = _path.GetSegmentPoint(i, 0f);
            for (int s = 1; s <= SamplesPerSegment; s++)
            {
                Vector3 cur = _path.GetSegmentPoint(i, (float)s / SamplesPerSegment);
                Handles.DrawLine(prev, cur, 3f);
                prev = cur;
            }
        }

        // 2. 绘制路径点 + 可拖动 handle
        for (int i = 0; i < _path.Points.Count; i++)
        {
            Handles.color = i == _selectedIndex ? Color.yellow : Color.white;
            Vector3 pos = _path.Points[i];
            EditorGUI.BeginChangeCheck();
            var fmh_80_17_639213751922941690 = Quaternion.identity; Vector3 newPos = Handles.FreeMoveHandle(
                pos,
                HandleSize * HandleUtility.GetHandleSize(pos),
                Vector3.zero,
                Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                _path.Points[i] = newPos;
                _selectedIndex = i;
                EditorUtility.SetDirty(_path);
            }

            // 点序号
            Handles.Label(pos + Vector3.up * (HandleSize * 3f), i.ToString());
        }

        // 3. 交互：Shift+点击追加点 / 点选 / Delete 删除
        HandleSceneEvents();
    }

    private void HandleSceneEvents()
    {
        Event e = Event.current;

        // Shift + 左键点击 → 追加新点
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.forward, _path.transform.position);
            if (plane.Raycast(ray, out float dist))
            {
                _path.Points.Add(ray.GetPoint(dist));
                _selectedIndex = _path.Points.Count - 1;
                EditorUtility.SetDirty(_path);
                e.Use();
            }
            return;
        }

        // 左键点击空白处 → 清空选中（仅当没有拖动手柄时）
        if (e.type == EventType.MouseDown && e.button == 0 && !_dragging)
        {
            _selectedIndex = -1;
            EditorUtility.SetDirty(_path);
        }

        // Delete → 删除选中的点
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && _selectedIndex >= 0)
        {
            _path.Points.RemoveAt(_selectedIndex);
            _selectedIndex = -1;
            EditorUtility.SetDirty(_path);
            e.Use();
        }

        // 记录拖动状态
        if (e.type == EventType.MouseDown && e.button == 0)
            _dragging = true;
        if (e.type == EventType.MouseUp)
            _dragging = false;
    }

    private Color SegmentColor(int i)
    {
        Vector2 dir = _path.GetSegmentDirection(i);
        if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x))
            return dir.y > 0f ? Color.green : Color.red;
        return Color.cyan;
    }

    private void AddPointAtEnd()
    {
        Vector3 last = _path.Points.Count > 0
            ? (Vector3)_path.Points[_path.Points.Count - 1]
            : _path.transform.position;
        _path.Points.Add(last + (Vector3)(Vector2.right * 1f));
        _selectedIndex = _path.Points.Count - 1;
        EditorUtility.SetDirty(_path);
    }
}
