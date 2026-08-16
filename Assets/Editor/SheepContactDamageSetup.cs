using UnityEditor;
using UnityEngine;

public static class SheepContactDamageSetup
{
    [MenuItem("Tools/给冲冲羊添加冲撞伤害")]
    public static void Add()
    {
        const string prefabPath = "Assets/Forms/Sheep/Prefabs/Sheep.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) { Debug.LogError("找不到 Sheep.prefab"); return; }

        ContactDamage cd = root.GetComponent<ContactDamage>();
        if (cd == null) cd = root.AddComponent<ContactDamage>();

        SerializedObject so = new SerializedObject(cd);
        so.FindProperty("targetLayer").intValue = 1 << LayerMask.NameToLayer("Player");
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("已给冲冲羊添加冲撞伤害组件");
    }
}