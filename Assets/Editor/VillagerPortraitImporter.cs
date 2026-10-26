using UnityEditor;

/// <summary>
/// Assets/Resources/Villagers/ の立ち絵を、取り込み時に自動で Sprite として設定する。
/// 組み込みの村人シナリオ（DefaultVillagerScenarios）が Resources.Load&lt;Sprite&gt; で読むため、
/// Texture のままだと null になり、代替エンブレムが表示され続けてしまう。
/// </summary>
public sealed class VillagerPortraitImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Villagers/";

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
