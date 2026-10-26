using UnityEditor;

/// <summary>
/// Assets/Resources/Chest/ の宝箱画像を、取り込み時に自動で Sprite として設定する。
/// ChestEventPanel が Resources.Load&lt;Sprite&gt; で読むため、Texture のままだと null になる。
/// </summary>
public sealed class ChestSpriteImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Chest/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
    }
}
