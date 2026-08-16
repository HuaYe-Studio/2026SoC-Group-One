using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnsureCoinManager
{
    [MenuItem("Tools/补充 CoinManager 到游戏场景")]
    public static void Add()
    {
        const string scenePath = "Assets/Scenes/WetLand_Main.unity";
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (Object.FindObjectOfType<CoinManager>() != null)
        {
            Debug.Log("场景中已有 CoinManager，跳过");
            return;
        }

        GameObject go = new GameObject("CoinManager");
        go.AddComponent<CoinManager>();
        Undo.RegisterCreatedObjectUndo(go, "Add CoinManager");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("已添加 CoinManager");
    }
}