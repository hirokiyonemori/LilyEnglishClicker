using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Resources/Backgrounds/ の背景画像を、取り込み時に自動で Sprite として設定する。
/// QuestBackgrounds が Resources.Load&lt;Sprite&gt; で読むため、Texture のままだと null になる。
/// 全画面の背景なので圧縮を効かせ、画面を覆うためメッシュは Full Rect にする。
/// </summary>
public sealed class BackgroundSpriteImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Backgrounds/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.maxTextureSize = 2048;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
