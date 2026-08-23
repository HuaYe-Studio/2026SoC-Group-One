using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MenuAndIconSetup
{
    private const string MenuScenePath = "Assets/Scenes/Scene_MainMenu.unity";
    private const string MenuBgPath = "Assets/Maps/Backgrounds/menu_bg.png";
    private const string IconPath = "Assets/UI/Sprites/app_icon.png";
    private const string BuildDir = "Build/Demo_20260823_menu";

    // Run this via batch mode: -executeMethod MenuAndIconSetup.Run
    public static void Run()
    {
        // 1) Reimport assets so import settings take effect
        AssetDatabase.ImportAsset(MenuBgPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);

        Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MenuBgPath);
        Texture2D iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        Debug.Log("[MenuIcon] bg loaded: " + (bgTex != null) + " size=" + (bgTex != null ? bgTex.width + "x" + bgTex.height : "null"));
        Debug.Log("[MenuIcon] icon loaded: " + (iconTex != null) + " size=" + (iconTex != null ? iconTex.width + "x" + iconTex.height : "null"));

        // 2) Set application icons for Standalone + WebGL
        if (iconTex != null)
        {
            SetIconsForGroup(BuildTargetGroup.Standalone, iconTex);
            SetIconsForGroup(BuildTargetGroup.WebGL, iconTex);
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuIcon] icons set");
        }
        else
        {
            Debug.LogError("[MenuIcon] icon texture failed to load");
        }

        // 3) Add background to main menu scene
        if (bgTex != null)
        {
            SetupMenuBackground();
        }
        else
        {
            Debug.LogError("[MenuIcon] menu bg texture failed to load");
        }
    }

    private static void SetIconsForGroup(BuildTargetGroup group, Texture2D icon)
    {
        int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(group);
        if (sizes == null || sizes.Length == 0)
        {
            PlayerSettings.SetIconsForTargetGroup(group, new Texture2D[] { icon });
            return;
        }
        Texture2D[] icons = new Texture2D[sizes.Length];
        for (int i = 0; i < sizes.Length; i++) icons[i] = icon;
        PlayerSettings.SetIconsForTargetGroup(group, icons);
        Debug.Log("[MenuIcon] group " + group + " sizes=" + string.Join(",", sizes));
    }

    private static void SetupMenuBackground()
    {
        SceneAsset menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        if (menuScene == null)
        {
            Debug.LogError("[MenuIcon] menu scene not found: " + MenuScenePath);
            return;
        }
        var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        // Find or create MenuBG
        Transform existing = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "MenuBG") { existing = root.transform; break; }
        }

        GameObject bgGO;
        if (existing != null)
        {
            bgGO = existing.gameObject;
            Debug.Log("[MenuIcon] MenuBG already exists, updating");
        }
        else
        {
            bgGO = new GameObject("MenuBG");
            Debug.Log("[MenuIcon] MenuBG created");
        }

        SpriteRenderer sr = bgGO.GetComponent<SpriteRenderer>();
        if (sr == null) sr = bgGO.AddComponent<SpriteRenderer>();
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBgPath);
        sr.sprite = bgSprite;
        sr.sortingOrder = -100;
        sr.color = Color.white;

        if (bgGO.GetComponent<MenuBackgroundFitter>() == null)
            bgGO.AddComponent<MenuBackgroundFitter>();

        // Camera: find Main Camera in scene
        Camera cam = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            cam = root.GetComponentInChildren<Camera>();
            if (cam != null) break;
        }
        if (cam != null)
        {
            bgGO.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z + 20f);
        }
        else
        {
            bgGO.transform.position = new Vector3(0f, 0f, 10f);
        }
        bgGO.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MenuIcon] menu background added and scene saved");
    }

    // Build demo via batch mode: -executeMethod MenuAndIconSetup.Build
    public static void Build()
    {
        Run();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        string[] scenes = {
            "Assets/Scenes/Scene_MainMenu.unity",
            "Assets/Scenes/WetLand_Main.unity",
            "Assets/Scenes/Scene_Boot.unity",
            "Assets/Scenes/SampleScene.unity"
        };
        string outDir = Path.Combine(Directory.GetCurrentDirectory(), BuildDir);
        Directory.CreateDirectory(outDir);
        string exePath = Path.Combine(outDir, "WetlandDemo.exe");

        var report = BuildPipeline.BuildPlayer(scenes, exePath, BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("[DemoBuild] result=" + (report.summary.result == BuildResult.Succeeded ? "SUCCESS" : "FAILED")
            + " errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings
            + " out=" + exePath);
        if (report.summary.result != BuildResult.Succeeded)
        {
            foreach (var msg in report.steps)
                foreach (var m in msg.messages)
                    Debug.Log("[DemoBuild] " + m.content);
        }
    }
}
