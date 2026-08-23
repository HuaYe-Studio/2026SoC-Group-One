using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OutdoorBgFix
{
    private const string ScenePath = "Assets/Scenes/WetLand_Main.unity";
    private static readonly string[] bgNames = { "BgSky", "BgFarMountain", "BgCanopy" };

    [MenuItem("Tools/修复室外背景跟随")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera cam = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            cam = root.GetComponentInChildren<Camera>();
            if (cam != null) break;
        }
        if (cam == null) { Debug.LogError("[BgFix] camera not found"); return; }
        Debug.Log("[BgFix] camera=" + cam.name + " ortho=" + cam.orthographicSize + " pos=" + cam.transform.position);

        foreach (string n in bgNames)
        {
            GameObject go = GameObject.Find(n);
            if (go == null) { Debug.LogWarning("[BgFix] " + n + " not found"); continue; }
            Transform t = go.transform;
            if (t.parent != cam.transform)
            {
                t.SetParent(cam.transform, false);
                Debug.Log("[BgFix] " + n + " reparented to camera");
            }
            // Cover the ortho view: height = ortho*2 + margin, width = height * aspect (use 16:9 margin)
            float h = cam.orthographicSize * 2f * 1.6f;
            float w = h * (16f / 9f) * 1.4f;
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Vector2 s = sr.sprite.bounds.size;
                float scale = Mathf.Max(w / s.x, h / s.y);
                t.localPosition = new Vector3(0f, 0f, 20f);
                t.localScale = new Vector3(scale, scale, 1f);
                Debug.Log("[BgFix] " + n + " scale=" + scale + " covers " + (s.x*scale) + "x" + (s.y*scale));
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BgFix] done, scene saved");
    }
}
