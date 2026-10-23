using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Resources/Music/ の曲に、BGM 向けの取り込み設定を自動で当てる。
///  ・BGM（bgm_*）  : Streaming（メモリを食わない）。ループ再生。
///  ・ジングル（jingle_*）: 短いので CompressedInMemory（鳴らす瞬間に遅れない）。
/// </summary>
public sealed class MusicClipImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Music/";

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Folder)) return;

        AudioImporter importer = (AudioImporter)assetImporter;
        bool jingle = System.IO.Path.GetFileName(assetPath).StartsWith("jingle_");

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = jingle ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = true;
    }
}
