using UnityEditor;
using UnityEngine;

public static class SheepFormSetup
{
    [MenuItem("Tools/完成冲冲羊形态（SheepForm + 可吞噬）")]
    public static void Setup()
    {
        AddSheepFormToPlayer();
        AddDevourableToSheep();
        AssetDatabase.SaveAssets();
        Debug.Log("冲冲羊形态完成：Player 增加 SheepForm，Sheep 可吞噬且吞噬后解锁羊形态");
    }

    private static void AddSheepFormToPlayer()
    {
        const string playerPath = "Assets/Forms/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
        if (root == null) { Debug.LogError("找不到 Player.prefab"); return; }

        if (root.transform.Find("SheepForm") != null)
        {
            Debug.Log("Player 已有 SheepForm，跳过");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Sprite placeholder = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Forms/Sheep/Sprites/sheep_placeholder.png");
        RuntimeAnimatorController sheepAnim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Forms/Sheep/Animations/Animator_Sheep.controller");

        GameObject sheepGo = new GameObject("SheepForm");
        sheepGo.layer = LayerMask.NameToLayer("Player");
        sheepGo.tag = "Player";
        sheepGo.transform.SetParent(root.transform, false);

        SpriteRenderer sr = sheepGo.AddComponent<SpriteRenderer>();
        sr.sprite = placeholder;

        BoxCollider2D col = sheepGo.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 0.8f);

        Animator anim = sheepGo.AddComponent<Animator>();
        anim.runtimeAnimatorController = sheepAnim;

        SheepForm sheepForm = sheepGo.AddComponent<SheepForm>();
        SerializedObject so = new SerializedObject(sheepForm);
        so.FindProperty("formType").enumValueIndex = (int)FormType.Sheep;
        so.ApplyModifiedPropertiesWithoutUndo();

        PlayerController pc = root.GetComponent<PlayerController>();
        if (pc != null)
        {
            SerializedObject pcSo = new SerializedObject(pc);
            SerializedProperty allForms = pcSo.FindProperty("allForms");
            allForms.InsertArrayElementAtIndex(allForms.arraySize);
            allForms.GetArrayElementAtIndex(allForms.arraySize - 1).objectReferenceValue = sheepForm;
            pcSo.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(root, playerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("已在 Player.prefab 添加 SheepForm");
    }

    private static void AddDevourableToSheep()
    {
        const string sheepPath = "Assets/Forms/Sheep/Prefabs/Sheep.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(sheepPath);
        if (root == null) { Debug.LogError("找不到 Sheep.prefab"); return; }

        DevourableAnimal dev = root.GetComponent<DevourableAnimal>();
        if (dev == null) dev = root.AddComponent<DevourableAnimal>();

        SerializedObject so = new SerializedObject(dev);
        so.FindProperty("grantedForm").enumValueIndex = (int)FormType.Sheep;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, sheepPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("已给 Sheep.prefab 添加 DevourableAnimal（grantedForm=Sheep）");
    }
}