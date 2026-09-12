using System.Text;

/// <summary>
/// [BT] 树布局导出器（阶段 E0.2）：公共可编辑模型 BTEditorNode → JSON 文本。
/// 输出格式与 BTLayoutParser.ParseLayout 完全兼容（字段名 type/name/params/children），
/// 编辑器导出 → 运行时直接加载，双向无损。
/// </summary>
public static class BTLayoutExporter
{
    /// <summary>导出整棵树（含顶层 root 包装）。</summary>
    public static string Export(BTEditorNode root)
    {
        if (root == null) return null;

        var sb = new StringBuilder(1024);
        sb.AppendLine("{");
        sb.AppendLine("  \"root\": {");
        WriteNode(sb, root, 2);
        sb.AppendLine();
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>递归写出节点（缩进格式化，空字段不导出；type 恒在首位，后续字段均以逗号前导）。</summary>
    private static void WriteNode(StringBuilder sb, BTEditorNode node, int depth)
    {
        string indent = new string(' ', depth * 2);

        sb.Append(indent).Append("\"type\": ").Append(Quote(node.type));

        if (!string.IsNullOrEmpty(node.name))
            sb.Append(",\n").Append(indent).Append("\"name\": ").Append(Quote(node.name));

        if (node.HasParams)
        {
            sb.Append(",\n").Append(indent).Append("\"params\": [");
            for (int i = 0; i < node.@params.Count; i++)
            {
                sb.Append(i > 0 ? ", " : "");
                sb.Append("{ \"key\": ").Append(Quote(node.@params[i].key))
                  .Append(", \"value\": ").Append(Quote(node.@params[i].value ?? string.Empty)).Append(" }");
            }
            sb.Append(']');
        }

        if (node.HasChildren)
        {
            sb.Append(",\n").Append(indent).Append("\"children\": [");
            for (int i = 0; i < node.children.Count; i++)
            {
                sb.Append(i > 0 ? ",\n" : "\n");
                sb.Append(indent).Append("  {\n");
                WriteNode(sb, node.children[i], depth + 2);
                sb.Append('\n').Append(indent).Append("  }");
            }
            sb.Append('\n').Append(indent).Append(']');
        }
    }

    /// <summary>JSON 字符串转义（引号/反斜杠/控制字符）。</summary>
    private static string Quote(string value)
    {
        if (value == null) return "\"\"";
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
