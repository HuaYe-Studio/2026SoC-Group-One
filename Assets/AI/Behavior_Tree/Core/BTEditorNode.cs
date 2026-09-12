using System;
using System.Collections.Generic;

/// <summary>
/// [BT] 可编辑树节点参数（阶段 E0.1）：键值对（JSON 兼容 JsonUtility，代替 Dictionary）。
/// 与运行时 BTLayoutParser 的 params 表格式一致，供编辑器参数面板编辑。
/// </summary>
[Serializable]
public class BTEditorParam
{
    public string key;
    public string value;

    public BTEditorParam() { }

    public BTEditorParam(string key, string value)
    {
        this.key = key;
        this.value = value;
    }
}

/// <summary>
/// [BT] 公共可编辑树模型（阶段 E0.1）：编辑器持有的"可编辑树数据"，
/// 与 BTLayoutParser 的 JSON 树格式完全一致（type/name/params/children）。
/// - 编辑器：拖拽/连线/配参的编辑对象；
/// - 运行时：BTLayoutParser.ParseLayout 解析 JSON 得到该模型 → 递归组装成行为树；
/// - 导出：BTLayoutExporter.Export 模型 → JSON，双向无损。
/// </summary>
[Serializable]
public class BTEditorNode
{
    /// <summary>节点类型名：工厂注册名（[BTNode("Selector")]）或逻辑叶子名（"ThreatUrgent"）或 "SubTree"。</summary>
    public string type;

    /// <summary>节点显示名（可选，调试/可视化用；JSON 中为空则不导出）。</summary>
    public string name;

    /// <summary>节点参数表（可选，对应运行时 GetParamFloat 等）。</summary>
    public List<BTEditorParam> @params;

    /// <summary>子树（组合节点子分支 / 装饰节点唯一子）。</summary>
    public List<BTEditorNode> children;

    public BTEditorNode() { }

    public BTEditorNode(string type, string name = null)
    {
        this.type = type;
        this.name = name;
    }

    // ==================== 便捷操作（编辑器用） ====================

    public bool HasParams => @params != null && @params.Count > 0;

    public bool HasChildren => children != null && children.Count > 0;

    public int ChildrenCount => children?.Count ?? 0;

    public BTEditorNode GetChild(int index) => children != null && index >= 0 && index < children.Count ? children[index] : null;

    /// <summary>读取参数值；不存在返回默认值。</summary>
    public string GetParam(string key, string defaultValue = null)
    {
        if (@params == null) return defaultValue;
        for (int i = 0; i < @params.Count; i++)
        {
            if (@params[i].key == key) return @params[i].value;
        }
        return defaultValue;
    }

    /// <summary>设置参数（存在则更新，否则追加）。</summary>
    public void SetParam(string key, string value)
    {
        if (@params == null) @params = new List<BTEditorParam>();
        for (int i = 0; i < @params.Count; i++)
        {
            if (@params[i].key == key)
            {
                @params[i].value = value;
                return;
            }
        }
        @params.Add(new BTEditorParam(key, value));
    }

    /// <summary>删除参数；返回是否删除了某项。</summary>
    public bool RemoveParam(string key)
    {
        if (@params == null) return false;
        for (int i = 0; i < @params.Count; i++)
        {
            if (@params[i].key == key)
            {
                @params.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>追加子节点。</summary>
    public BTEditorNode AddChild(BTEditorNode child)
    {
        if (children == null) children = new List<BTEditorNode>();
        if (child != null) children.Add(child);
        return this;
    }

    /// <summary>移除子节点。</summary>
    public bool RemoveChild(BTEditorNode child)
    {
        return children != null && children.Remove(child);
    }

    /// <summary>整棵子树深拷贝（"另存为"/复制粘贴用），与原树完全独立。</summary>
    public BTEditorNode Clone()
    {
        var copy = new BTEditorNode(type, name);
        if (@params != null)
        {
            copy.@params = new List<BTEditorParam>(@params.Count);
            for (int i = 0; i < @params.Count; i++)
                copy.@params.Add(new BTEditorParam(@params[i].key, @params[i].value));
        }
        if (children != null)
        {
            copy.children = new List<BTEditorNode>(children.Count);
            for (int i = 0; i < children.Count; i++)
                copy.children.Add(children[i] != null ? children[i].Clone() : null);
        }
        return copy;
    }
}
