#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// [BT] 编辑器视觉样式常量：行为树节点分类 → 颜色、连线样式。
/// 供编辑画布（EditNodeView）/ 查看画布 / 参数面板图例共用，保证全编辑器配色一致。
/// </summary>
public static class BTEditorStyle
{
    /// <summary>节点分类 → 标题背景色（配黑字，需保证足够亮）。</summary>
    public static Color ColorFor(BTNodeCategory category)
    {
        return category switch
        {
            BTNodeCategory.Composite => new Color(0.32f, 0.56f, 0.85f),   // 组合：蓝
            BTNodeCategory.Decorator => new Color(0.66f, 0.50f, 0.84f),   // 装饰：紫
            BTNodeCategory.Registered => new Color(0.30f, 0.72f, 0.58f),  // 注册(任务)：青绿
            BTNodeCategory.Leaf => new Color(0.93f, 0.62f, 0.30f),        // 逻辑叶子：橙
            _ => new Color(0.55f, 0.58f, 0.62f),                          // 子树：灰
        };
    }

    /// <summary>编辑连线颜色（清晰可见的浅灰）。</summary>
    public static Color EdgeColor => new Color(0.72f, 0.74f, 0.78f);

    /// <summary>编辑连线宽度。</summary>
    public const float EdgeWidth = 2f;

    /// <summary>分类中文名（面板/图例用）。</summary>
    public static string DisplayName(BTNodeCategory category)
    {
        return category switch
        {
            BTNodeCategory.Composite => "组合",
            BTNodeCategory.Decorator => "装饰",
            BTNodeCategory.Registered => "任务",
            BTNodeCategory.Leaf => "叶子",
            _ => "子树",
        };
    }
}
#endif
