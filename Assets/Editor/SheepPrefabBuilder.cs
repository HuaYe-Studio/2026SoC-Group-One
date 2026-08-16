using System.IO;
using UnityEditor;
using UnityEngine;

public static class SheepPrefabBuilder
{
    private const string PrefabPath = "Assets/Forms/Sheep/Prefabs/Sheep.prefab";
    private const string PlaceholderSpritePath = "Assets/Forms/Sheep/Sprites/sheep_placeholder.png";

    [MenuItem("Tools/生成冲冲羊 Sheep 预制体")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog("Sheep 已存在", "Sheep.prefab 已存在，是否重新生成？", "重新生成", "取消"))
                return;
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        EnsureFolder(Path.GetDirectoryName(PrefabPath));
        EnsureFolder(Path.GetDirectoryName(PlaceholderSpritePath));

        Sprite sprite = EnsurePlaceholderSprite();

        GameObject go = new GameObject("Sheep");
        go.layer = LayerMask.NameToLayer("Animal");
        go.tag = "Animal";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        BoxCollider2D body = go.AddComponent<BoxCollider2D>();
        body.size = new Vector2(1f, 0.8f);

        Animator animator = go.AddComponent<Animator>();

        SheepAI sheepAI = go.AddComponent<SheepAI>();
        SheepBT sheepBT = go.AddComponent<SheepBT>();

        var so = new SerializedObject(sheepAI);
        so.FindProperty("_animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnvironmentMonitor monitor = go.GetComponent<EnvironmentMonitor>();
        if (monitor != null)
        {
            var monSo = new SerializedObject(monitor);
            SetLayerMask(monSo, "_threatLayer", "Player");
            SetLayerMask(monSo, "_groundLayer", "Default");
            SetLayerMask(monSo, "_wallLayer", "Default");
            monSo.ApplyModifiedPropertiesWithoutUndo();
        }

        RevengeBehavior revenge = go.GetComponent<RevengeBehavior>();
        if (revenge != null)
        {
            var revSo = new SerializedObject(revenge);
            revSo.FindProperty("_kinTag").stringValue = "Animal";
            revSo.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Debug.Log("Sheep 预制体已生成: " + PrefabPath);
    }

    private static void SetLayerMask(SerializedObject so, string fieldName, string layerName)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null) return;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) { Debug.LogWarning("图层 " + layerName + " 不存在，" + fieldName + " 保持默认"); return; }
        prop.intValue = 1 << layer;
    }

    private static Sprite EnsurePlaceholderSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        if (existing != null) return existing;

        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(PlaceholderSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(PlaceholderSpritePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderSpritePath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }
}