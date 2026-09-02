#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// [BT] 简易输入框弹窗（新建树命名等场景）。Unity 无内置单行输入对话框，用它代替。
/// </summary>
public class InputDialog : EditorWindow
{
    private string _title = "";
    private string _initial = "";
    private string _value = "";
    private System.Action<string> _onOk;

    public static void Show(string title, string initial, System.Action<string> onOk)
    {
        var win = CreateInstance<InputDialog>();
        win._title = title;
        win._initial = initial;
        win._value = initial;
        win._onOk = onOk;
        win.titleContent = new GUIContent(title);
        win.minSize = new Vector2(320f, 90f);
        win.maxSize = new Vector2(320f, 90f);
        win.ShowUtility();
        win.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(_title);
        EditorGUILayout.Space(4);

        GUI.SetNextControlName("Input");
        _value = EditorGUILayout.TextField(_value);
        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("确定"))
        {
            var ok = _onOk;
            _onOk = null;
            Close();
            ok?.Invoke(_value);
        }
        if (GUILayout.Button("取消"))
        {
            _onOk = null;
            Close();
        }
        EditorGUILayout.EndHorizontal();

        // 聚焦输入框，回车确认
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                var ok = _onOk;
                _onOk = null;
                Close();
                ok?.Invoke(_value);
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Escape)
            {
                _onOk = null;
                Close();
                Event.current.Use();
            }
        }

        if (Event.current.type == EventType.Repaint)
        {
            var focused = GUI.GetNameOfFocusedControl();
            if (focused != "Input")
            {
                GUI.FocusControl("Input");
                EditorGUI.FocusTextInControl("Input");
            }
        }
    }
}
#endif
