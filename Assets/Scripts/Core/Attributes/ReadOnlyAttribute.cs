using UnityEngine;

/// <summary>
/// 标记字段在 Inspector 中只读显示（运行时调试数据用）。
/// 配合 Editor 下的 ReadOnlyDrawer 使用。
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }
