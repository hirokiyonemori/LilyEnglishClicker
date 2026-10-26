using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Image/UI/ に置かれたバッジ用アイコンへ共通のインポート設定を適用する。
///
/// 手でメタファイルのspriteModeを書き換えただけではUnity側の
/// 再インポートが確実に走るとは限らないため、このメニューから
/// TextureImporter APIを通して確実に設定し直せるようにしてある。
/// </summary>
public static class UIIconImporter
{
    private const string IconFolder = "Assets/Image/UI";
    private const int TargetSize = 128;

    [MenuItem("Tools/LilyClicker/UIアイコンのインポート設定を適用")]
    public static void ApplyToAllMenu()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder))
        {
            Debug.LogWarning($"[UIIconImporter] フォルダが存在しません: {IconFolder}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder });
        int applied = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = TargetSize;

                // アイコンは足元pivotの敵と違い、中央基準でよい
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings);

                ApplyPlatformSettings(importer, "Android");
                ApplyPlatformSettings(importer, "iPhone");

                importer.SaveAndReimport();
                applied++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[UIIconImporter] {applied}件のアイコンに設定を適用しました。");
    }

    private static void ApplyPlatformSettings(TextureImporter importer, string platform)
    {
        TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
        platformSettings.overridden = true;
        platformSettings.maxTextureSize = TargetSize;
        platformSettings.format = TextureImporterFormat.ASTC_6x6;
        platformSettings.compressionQuality = (int)TextureCompressionQuality.Normal;
        importer.SetPlatformTextureSettings(platformSettings);
    }
}
