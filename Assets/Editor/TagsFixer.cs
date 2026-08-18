using UnityEditor;
using UnityEngine;

public static class TagsFixer
{
    [MenuItem("Tools/补全标签并修复 Sheep 标签")]
    public static void Fix()
    {
        EnsureTag("Player");
        EnsureTag("Animal");
        SetPrefabTag("Assets/Forms/Sheep/Prefabs/Sheep.prefab", "Animal");
        AssetDatabase.SaveAssets();
        Debug.Log("标签修复完成：Player、Animal 标签已存在，Sheep 标签已设为 Animal");
    }

    private static readonly string[] BuiltInDefaultTags =
    {
        "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController"
    };

    private static void EnsureTag(string tag)
    {
        if (System.Array.IndexOf(BuiltInDefaultTags, tag) >= 0)
        {
            Debug.Log("跳过内置默认标签: " + tag);
            return;
        }
        Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject so = new SerializedObject(tagManager);
        SerializedProperty tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        }
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        Debug.Log("已添加标签: " + tag);
    }

    private static void SetPrefabTag(string prefabPath, string tag)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) { Debug.LogError("找不到预制体 " + prefabPath); return; }
        root.tag = tag;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("已设置 " + prefabPath + " 标签为 " + tag);
    }
}