#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 湿地场景美术：批量设置图块导入参数。
/// 用法：Unity 菜单 -> Tools -> 湿地美术 -> 批量设置图块导入。
/// 规则：
///  - 32x32 单张图 -> Sprite Single
///  - 更大尺寸(精灵表) -> Sprite Multiple，按 32x32 网格切片
///  - 统一 PPU=32、Bilinear、无 mipmap、不压缩
/// </summary>
public static class WetlandTileImportSetup
{
    const string TilemapFolder = "Assets/Maps/Tilemaps";
    const int TileSize = 32;
    const int PPU = 32;

    [MenuItem("Tools/湿地美术/批量设置图块导入(32x32/PPU32)")]
    public static void SetupAllTilemaps()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TilemapFolder });
        int handled = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = PPU;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            // 读取实际图片尺寸，决定 Single / Multiple
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null && tex.width == TileSize && tex.height == TileSize)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }
            else if (tex != null)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                int cols = tex.width / TileSize;
                int rows = tex.height / TileSize;
                if (cols < 1) cols = 1;
                if (rows < 1) rows = 1;

                var metas = new SpriteMetaData[cols * rows];
                int index = 0;
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        metas[index] = new SpriteMetaData
                        {
                            name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + x + "_" + y,
                            rect = new Rect(x * TileSize, (rows - 1 - y) * TileSize, TileSize, TileSize),
                            pivot = new Vector2(0.5f, 0.5f),
                            alignment = (int)SpriteAlignment.Center
                        };
                        index++;
                    }
                }
                importer.spritesheet = metas;
            }

            importer.SaveAndReimport();
            handled++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[湿地美术] 图块导入设置完成，处理 " + handled + " 张：" + TilemapFolder);
    }
}
#endif