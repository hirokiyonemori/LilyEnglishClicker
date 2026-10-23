using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Image/Enemy/ に置かれた敵スプライトへ共通のインポート設定を適用する。
///
/// 新規に追加されたPNGは OnPreprocessTexture で自動的に設定されるが、
/// 先に取り込まれてしまったファイルには効かないため、
/// メニューから明示的に適用し直せるようにしてある。
/// </summary>
public class EnemySpriteImporter : AssetPostprocessor
{
    public const string EnemyFolder = "Assets/Image/Enemy";

    // ティア4以上の大型キャラのみ1024を維持する。それ以外は512で十分
    private static readonly HashSet<string> HighResEnemies = new HashSet<string>
    {
        "Enemy_StoneGolem",
        "Enemy_Gargoyle",
        "Enemy_DarkHelm",
        "Enemy_DragonWhelp",
        "Enemy_WordDevil",
        "10", // WordDevil
        "22", // Gargoyle
        "23", // DragonWhelp
        "24", // DarkHelm
    };

    /// <summary>新規インポート時の自動適用</summary>
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(EnemyFolder + "/"))
        {
            return;
        }

        // 初回インポート時のみ。既存アセットの手動調整を上書きしない
        if (!assetImporter.importSettingsMissing)
        {
            return;
        }

        Configure(assetImporter as TextureImporter, assetPath);
    }

    [MenuItem("Tools/LilyClicker/敵スプライトのインポート設定を適用")]
    public static void ApplyToAllMenu()
    {
        int applied = ApplyToAll();
        Debug.Log($"[EnemySpriteImporter] {applied}件のスプライトに設定を適用しました。");
    }

    /// <summary>
    /// フォルダ内の全PNGへ設定を適用し直す。適用した件数を返す
    /// </summary>
    public static int ApplyToAll()
    {
        if (!AssetDatabase.IsValidFolder(EnemyFolder))
        {
            Debug.LogWarning($"[EnemySpriteImporter] フォルダが存在しません: {EnemyFolder}");
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { EnemyFolder });
        int applied = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                {
                    continue;
                }

                Configure(importer, path);
                importer.SaveAndReimport();
                applied++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        return applied;
    }

    /// <summary>
    /// 敵スプライト共通のインポート設定を書き込む
    /// </summary>
    private static void Configure(TextureImporter importer, string path)
    {
        if (importer == null)
        {
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        int maxSize = HighResEnemies.Contains(fileName) ? 1024 : 512;

        importer.textureType = TextureImporterType.Sprite;
        // Multipleのままだと Sprite として読み込めないため必ずSingleにする
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f; // 既存アセットと統一
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = maxSize;

        // 足元pivot（キャラの接地点を基準に配置できるようにする）
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);

        ApplyPlatformSettings(importer, "Android", maxSize);
        ApplyPlatformSettings(importer, "iPhone", maxSize);
    }

    private static void ApplyPlatformSettings(TextureImporter importer, string platform, int maxSize)
    {
        TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
        platformSettings.overridden = true;
        platformSettings.maxTextureSize = maxSize;
        platformSettings.format = TextureImporterFormat.ASTC_6x6;
        platformSettings.compressionQuality = (int)TextureCompressionQuality.Normal;
        importer.SetPlatformTextureSettings(platformSettings);
    }
}
