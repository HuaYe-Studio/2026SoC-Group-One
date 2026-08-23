using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TempleBgSetup
{
    private const string ScenePath = "Assets/Scenes/WetLand_Main.unity";
    private const string ExtPath = "Assets/Maps/Backgrounds/temple_exterior_bg.png";
    private const string IntPath = "Assets/Maps/Backgrounds/temple_interior_bg.png";

    [MenuItem("Tools/接入神殿背景")]
    public static void Run()
    {
        AssetDatabase.ImportAsset(ExtPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(IntPath, ImportAssetOptions.ForceUpdate);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Sprite extSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ExtPath);
        Sprite intSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IntPath);
        Debug.Log("[TempleBg] ext sprite=" + (extSprite != null) + " int sprite=" + (intSprite != null));

        // Find the Background parent (holds BG1 snake den bg)
        Transform bgParent = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Background") { bgParent = root.transform; break; }
        }
        if (bgParent == null) { Debug.LogError("[TempleBg] Background parent not found"); return; }

        AddBg(bgParent, "TempleExteriorBG", extSprite, new Vector2(565f, 31f), 100f, 44f, -202);
        AddBg(bgParent, "TempleInteriorBG", intSprite, new Vector2(690f, 27f), 170f, 44f, -201);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TempleBg] done, scene saved");
    }

    private static void AddBg(Transform parent, string objName, Sprite sprite, Vector2 center, float worldW, float worldH, int order)
    {
        // find existing
        Transform existing = null;
        foreach (Transform child in parent)
        {
            if (child.name == objName) { existing = child; break; }
        }
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else { go = new GameObject(objName); go.transform.SetParent(parent, false); }

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = Color.white;

        if (sprite == null) return;
        Vector2 s = sprite.bounds.size;
        if (s.x <= 0f || s.y <= 0f) return;
        float scale = Mathf.Max(worldW / s.x, worldH / s.y) * 1.05f;
        go.transform.localPosition = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        Debug.Log("[TempleBg] " + objName + " center=" + center + " scale=" + scale + " covers " + (s.x*scale) + "x" + (s.y*scale));
    }
}
