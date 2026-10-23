using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 効果音（SE）を名前で鳴らす。MusicDirector の SE 版。
///
/// ・音は Assets/Resources/Sound/ の AudioClip をファイル名で読む（シーン・Prefab の割り当ては不要）。
///     se_attack … 攻撃（斬撃）
///     se_magic  … 魔法
/// ・音が無ければ何もしない（ゲームの流れには関与しない）。
/// ・使い方: SfxDirector.Play(SfxDirector.Attack);
/// ・初回の Play で自動的に生成され、シーンをまたいで残る。
/// </summary>
public sealed class SfxDirector : MonoBehaviour
{
    public const string Attack = "se_attack";
    public const string Magic = "se_magic";
    public const string Hit = "se_hit";
    public const string Guard = "se_guard";
    public const string ChestOpen = "se_chest_open";

    private const string ResourceFolder = "Sound/";
    private const string VolumeKey = "LilyClicker.SeVolume";
    private const float DefaultVolume = 0.8f;
    private const int VoiceCount = 4;

    private static SfxDirector instance;
    private static float volume = -1f;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource[] voices;
    private int nextVoice;

    /// <summary>SE の音量（0〜1）。端末に保存され、将来の設定画面からも変えられる</summary>
    public static float Volume
    {
        get
        {
            if (volume < 0f) volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
            return volume;
        }
        set
        {
            volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, volume);
        }
    }

    /// <summary>名前の SE を 1 回鳴らす。音が見つからなければ false（何も起きない）</summary>
    public static bool Play(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return false;
        if (instance == null)
        {
            // 破棄済みの参照を残さないため、Unity の == で判定する
            instance = new GameObject("[SfxDirector]").AddComponent<SfxDirector>();
        }
        return instance.PlayInternal(clipName);
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < voices.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private bool PlayInternal(string clipName)
    {
        AudioClip clip = Load(clipName);
        if (clip == null) return false;

        // 連打しても前の音を切らないよう、複数のボイスを順番に使う
        AudioSource source = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        source.PlayOneShot(clip, Volume);
        return true;
    }

    private AudioClip Load(string clipName)
    {
        AudioClip clip;
        if (clips.TryGetValue(clipName, out clip)) return clip;
        clip = Resources.Load<AudioClip>(ResourceFolder + clipName);
        clips[clipName] = clip;   // 無い場合も覚えて、毎回 Resources を探さない
        return clip;
    }
}
