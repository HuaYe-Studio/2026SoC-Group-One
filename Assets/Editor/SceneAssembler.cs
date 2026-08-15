using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Cinemachine;

/// <summary>
/// 一键组装 WetLand_Main 场景：
/// 自动加入 EventSystem / GameManager / Player / AudioManager / HUD UI / Cinemachine 相机跟随 / 吞噬特写组件。
/// 用法：菜单栏 Tools -> 组装 WetLand_Main 场景
/// </summary>
public static class SceneAssembler
{
    private const string ScenePath = "Assets/Scenes/WetLand_Main.unity";

    private const string PlayerPrefabPath = "Assets/Forms/Player.prefab";
    private const string GameManagerPrefabPath = "Assets/Mechanics/Prefabs/GameManager.prefab";

    // 玩家出生点（可按地图调整，默认在相机所在起点附近）
    private static readonly Vector3 PlayerSpawn = new Vector3(-32.7f, 2.2f, 0f);

    private static readonly string[] HudPrefabPaths =
    {
        "Assets/UI/Prefabs/PF_UI_HP.prefab",
        "Assets/UI/Prefabs/PF_UI_FormWheel.prefab",
        "Assets/UI/Prefabs/PF_UI_FormProgress.prefab",
        "Assets/UI/Prefabs/PF_UI_DevourTip.prefab",
        "Assets/UI/Prefabs/PF_UI_CoinCollect.prefab",
        "Assets/UI/Prefabs/PF_UI_PausePage.prefab",
        "Assets/UI/Prefabs/PF_UI_SceneTransition.prefab",
    };

    [MenuItem("Tools/组装 WetLand_Main 场景")]
    public static void Assemble()
    {
        // 1. 打开目标场景
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 2. EventSystem（新输入系统 UI 模块）
        EnsureEventSystem();

        // 3. GameManager（PlayerInputReader 等单例）
        EnsurePrefabInstance(GameManagerPrefabPath, "GameManager");

        // 4. Player（出生点）
        GameObject player = EnsurePrefabInstance(PlayerPrefabPath, "Player");
        if (player != null)
        {
            player.transform.position = PlayerSpawn;
            if (!player.CompareTag("Player")) player.tag = "Player";
        }

        // 5. AudioManager
        EnsureAudioManager();

        // 6. HUD UI
        foreach (string path in HudPrefabPaths)
            EnsurePrefabInstance(path, Path.GetFileNameWithoutExtension(path));

        // 7. 相机：CinemachineBrain + 虚拟相机跟随 + 滚轮缩放 + 吞噬特写
        Camera mainCam = Camera.main;
        if (mainCam == null) { Debug.LogError("场景里没有 Main Camera，请先创建相机"); return; }
        mainCam.gameObject.tag = "MainCamera";
        if (mainCam.GetComponent<CinemachineBrain>() == null)
            mainCam.gameObject.AddComponent<CinemachineBrain>();
        if (mainCam.GetComponent<DevourEffectPlayer>() == null)
            mainCam.gameObject.AddComponent<DevourEffectPlayer>();

        GameObject vcamGo = GameObject.Find("CM vcam1");
        if (vcamGo == null) vcamGo = new GameObject("CM vcam1");
        CinemachineVirtualCamera vcam = vcamGo.GetComponent<CinemachineVirtualCamera>();
        if (vcam == null) vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
        vcam.m_Lens.Orthographic = true;
        vcam.m_Lens.OrthographicSize = 5f;
        vcam.Follow = player != null ? player.transform : null;
        CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null) transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_FollowOffset = new Vector3(0f, 0f, -10f);
        if (vcamGo.GetComponent<CameraController>() == null)
            vcamGo.AddComponent<CameraController>();
        if (player != null)
            vcamGo.transform.position = player.transform.position + Vector3.forward * -10f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("场景组装完成！请在编辑器中检查：\n" +
                  "1) Player 出生点位置是否合适（可拖动调整）\n" +
                  "2) GameManager / AudioManager / EventSystem / HUD 是否都在 Hierarchy\n" +
                  "3) CM vcam1 是否跟随 Player\n" +
                  "4) 主相机上是否有 CinemachineBrain 和 DevourEffectPlayer");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        Debug.Log("已创建 EventSystem");
    }

    private static void EnsureAudioManager()
    {
        if (GameObject.Find("AudioManager") != null) return;
        GameObject go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
        Debug.Log("已创建 AudioManager");
    }

    private static GameObject EnsurePrefabInstance(string prefabPath, string fallbackName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"找不到预制体: {prefabPath}");
            return null;
        }

        if (PrefabAlreadyInScene(prefab))
        {
            Debug.Log($"场景中已存在 {prefab.name}，跳过");
            return GameObject.Find(prefab.name);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
        {
            Debug.LogError($"实例化失败: {prefabPath}");
            return null;
        }
        Undo.RegisterCreatedObjectUndo(instance, "Assemble Scene");
        Debug.Log($"已加入 {instance.name}");
        return instance;
    }

    private static bool PrefabAlreadyInScene(GameObject prefab)
    {
        GameObject[] all = Object.FindObjectsOfType<GameObject>(true);
        foreach (GameObject go in all)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab)
                return true;
        }
        return false;
    }
}