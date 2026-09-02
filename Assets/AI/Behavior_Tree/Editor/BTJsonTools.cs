using UnityEditor;
using UnityEngine;

/// <summary>
/// [BT] JSON 树工具（阶段 E0.2 验证）：验证 Resources/BTTrees/*.json 的
/// ParseLayout → Export → ParseLayout 往返无损（编辑器导出与运行时加载格式兼容）。
/// 菜单：Tools/BT/验证 JSON 往返。
/// </summary>
public static class BTJsonTools
{
    [MenuItem("Tools/BT/验证 JSON 往返")]
    private static void ValidateRoundTrip()
    {
        var assets = Resources.LoadAll<TextAsset>("BTTrees");
        if (assets == null || assets.Length == 0)
        {
            Debug.Log("[BTJsonTools] Resources/BTTrees 下没有 JSON 树资产");
            return;
        }

        int pass = 0, fail = 0;
        foreach (var asset in assets)
        {
            bool ok = ValidateOne(asset.name, asset.text);
            if (ok) pass++; else fail++;
        }

        Debug.Log($"[BTJsonTools] 往返验证完成：{pass} 通过 / {fail} 失败（共 {assets.Length} 棵树）");
        if (fail > 0)
            EditorUtility.DisplayDialog("BT JSON 往返验证", $"{fail} 棵树往返验证失败，详见 Console。", "知道了");
    }

    private static bool ValidateOne(string treeName, string json)
    {
        BTEditorNode root1 = BTLayoutParser.ParseLayout(json);
        if (root1 == null)
        {
            Debug.LogError($"[BTJsonTools] {treeName}：原始 JSON 解析失败");
            return false;
        }

        string exported = BTLayoutExporter.Export(root1);
        BTEditorNode root2 = BTLayoutParser.ParseLayout(exported);
        if (root2 == null)
        {
            Debug.LogError($"[BTJsonTools] {treeName}：导出 JSON 解析失败——导出器格式不兼容：\n{exported}");
            return false;
        }

        if (SameTree(root1, root2))
        {
            Debug.Log($"[BTJsonTools] {treeName}：往返通过（{CountNodes(root1)} 节点）");
            return true;
        }

        Debug.LogError($"[BTJsonTools] {treeName}：往返结构不一致（解析→导出→再解析 丢失信息）");
        return false;
    }

    /// <summary>递归比较两棵模型树（params 按键值对比较，与顺序无关）。</summary>
    private static bool SameTree(BTEditorNode a, BTEditorNode b)
    {
        if (a == null || b == null) return a == b;
        if (a.type != b.type) return false;
        if (a.name != b.name) return false;
        if (!SameParams(a, b)) return false;
        if (a.ChildrenCount != b.ChildrenCount) return false;
        for (int i = 0; i < a.ChildrenCount; i++)
        {
            if (!SameTree(a.children[i], b.children[i])) return false;
        }
        return true;
    }

    private static bool SameParams(BTEditorNode a, BTEditorNode b)
    {
        // 收集 a 的所有键
        if (a.@params == null && b.@params == null) return true;
        if (a.@params == null || b.@params == null) return a.HasParams == b.HasParams;

        if (a.@params.Count != b.@params.Count) return false;
        const string Missing = "\u0000_BT_MISSING_\u0000"; // 哨兵：GetParam 缺键时返回
        foreach (var pa in a.@params)
        {
            string bv = b.GetParam(pa.key, Missing);
            if (ReferenceEquals(bv, Missing)) return false; // b 缺该键
            if (bv != pa.value) return false;
        }
        return true;
    }

    private static int CountNodes(BTEditorNode node)
    {
        int count = 1;
        if (node.children != null)
        {
            foreach (var c in node.children) count += CountNodes(c);
        }
        return count;
    }
}
