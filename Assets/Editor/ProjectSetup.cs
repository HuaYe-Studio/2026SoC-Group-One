using UnityEditor;
using UnityEngine;

public static class ProjectSetup
{
    [MenuItem("Tools/配置动画控制器")]
    public static void ConfigureAnimators()
    {
        AssignPlayerFormAnimators();
        AssignNpcAnimator("Assets/Forms/Frog/Prefabs/Frog.prefab", "Assets/Forms/Frog/Animations/Frog.controller");
        AssignNpcAnimator("Assets/Forms/BubbleFish/Prefabs/BubbleFish.prefab", "Assets/Forms/BubbleFish/Animations/Animator_BubbleFishForm.controller");
        AssetDatabase.SaveAssets();
        Debug.Log("动画控制器配置完成");
    }

    [MenuItem("Tools/添加场景到 Build Settings")]
    public static void AddScenesToBuild()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Scene_MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/WetLand_Main.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Scene_Boot.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
        };
        Debug.Log("已更新 Build Settings（入口：Scene_MainMenu）");
    }

    private static void AssignPlayerFormAnimators()
    {
        const string playerPath = "Assets/Forms/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
        if (root == null) { Debug.LogError("找不到 Player.prefab"); return; }

        AssignAnimatorToChild(root, "SlimeForm", "Assets/Forms/Slime/Animations/Animator_SlimeForm.controller");
        AssignAnimatorToChild(root, "FrogForm", "Assets/Forms/Frog/Animations/Animator_FrogForm.controller");
        AssignAnimatorToChild(root, "BubbleFishForm", "Assets/Forms/BubbleFish/Animations/Animator_BubbleFishForm.controller");

        PrefabUtility.SaveAsPrefabAsset(root, playerPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AssignAnimatorToChild(GameObject root, string childName, string controllerPath)
    {
        GameObject child = FindChild(root, childName);
        if (child == null) { Debug.LogWarning("未找到子物体 " + childName); return; }

        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (ctrl == null) { Debug.LogError("找不到控制器 " + controllerPath); return; }

        Animator anim = child.GetComponent<Animator>();
        if (anim == null) anim = child.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        Debug.Log("已指定 " + childName + " 动画控制器");
    }

    private static void AssignNpcAnimator(string prefabPath, string controllerPath)
    {
        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (ctrl == null) { Debug.LogError("找不到控制器 " + controllerPath); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) { Debug.LogError("找不到预制体 " + prefabPath); return; }

        Animator anim = root.GetComponent<Animator>();
        if (anim == null) anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("已指定 " + prefabPath + " 动画控制器");
    }

    private static GameObject FindChild(GameObject root, string name)
    {
        Transform direct = root.transform.Find(name);
        if (direct != null) return direct.gameObject;
        foreach (Transform child in root.transform)
        {
            GameObject found = FindChild(child.gameObject, name);
            if (found != null) return found;
        }
        return null;
    }
}