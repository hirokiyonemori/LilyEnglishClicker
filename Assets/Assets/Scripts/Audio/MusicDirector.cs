using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 場面に合わせて BGM を切り替える（本編 / クエスト / ボス戦 / クリア）。
///
/// ・曲は Assets/Resources/Music/ の AudioClip をファイル名で読む（シーン・Prefab の割り当ては不要）。
///     bgm_field  … 本編（クエスト外）
///     bgm_quest  … クエスト中（選択・村人・宝箱）
///     bgm_boss   … 道中戦闘・ボス戦（既存の戦闘曲を共用）
///     jingle_clear / jingle_gameover … クリア・ゲームオーバー（1回だけ鳴らす）
/// ・曲が無い場面は近い曲へ代用する（boss → quest → field、clear → quest → field）。何も無ければ無音。
/// ・切り替えは 2 つの AudioSource のクロスフェード。進行状態は QuestManager から読むだけで、ゲームの流れには関与しない。
/// ・Game / Quest シーンで自動的に生成される。それ以外のシーンでは自分で消える。
/// </summary>
public sealed class MusicDirector : MonoBehaviour
{
    public const string Field = "bgm_field";
    public const string Quest = "bgm_quest";
    public const string Boss = "bgm_boss";
    public const string Clear = "jingle_clear";
    public const string GameOver = "jingle_gameover";

    private const string VolumeKey = "LilyClicker.BgmVolume";
    private const float DefaultVolume = 0.45f;
    private const string JinglePrefix = "jingle_";

    [Min(0.05f)] public float fadeSeconds = 1.0f;

    private sealed class Channel
    {
        public AudioSource source;
        public float level;     // 0〜1。フェードの進み具合（実際の音量は level × Volume）
        public bool wanted;
    }

    private static float volume = -1f;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Channel[] channels = new Channel[2];
    private Channel active;
    private AudioClip currentClip;

    public static MusicDirector Instance { get; private set; }

    /// <summary>いま流している曲の名前（代用が働いた場合は代用先の名前）。無音なら null</summary>
    public string CurrentTrack { get; private set; }

    public bool IsLooping { get { return active != null && active.source != null && active.source.loop; } }

    /// <summary>フェード完了後の実際の出力音量（0〜1）</summary>
    public float CurrentOutputVolume { get { return active != null && active.source != null ? active.source.volume : 0f; } }

    /// <summary>BGM の音量（0〜1）。端末に保存され、将来の設定画面からも変えられる</summary>
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;
        new GameObject("[MusicDirector]").AddComponent<MusicDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < channels.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            channels[i] = new Channel { source = source };
        }
    }

    private void Start()
    {
        // クリッカー本編でもクエストでもないシーンでは何もしない
        if (FindFirstObjectByType<Idle.GameManager>() == null && FindFirstObjectByType<QuestManager>() == null)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        AudioClip clip;
        string track = Resolve(Desired(), out clip);

        if (track != CurrentTrack || clip != currentClip)
        {
            Switch(track, clip);
        }

        TickFades();
    }

    // ─── 公開 API ────────────────────────────────────────────────
    /// <summary>曲を差し替える（Resources より優先）。外部の仕組みやテストから使う</summary>
    public void RegisterClip(string name, AudioClip clip)
    {
        clips[name] = clip;
    }

    public void UnregisterClip(string name)
    {
        clips.Remove(name);
    }

    // ─── 場面 → 曲 ───────────────────────────────────────────────
    private static string Desired()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null || !QuestManager.IsQuestActive) return Field;

        switch (quest.CurrentState)
        {
            case QuestState.EnemyBattle:
            case QuestState.BossBattle: return Boss;
            case QuestState.QuestClear: return Clear;
            case QuestState.GameOver:   return GameOver;
            default:                    return Quest;
        }
    }

    /// <summary>希望の曲が無ければ、近い曲へ順に代用する</summary>
    private string Resolve(string wanted, out AudioClip clip)
    {
        string name = wanted;
        for (int i = 0; i < 4 && name != null; i++)
        {
            clip = Load(name);
            if (clip != null) return name;
            name = Fallback(name);
        }

        clip = null;
        return null;
    }

    private static string Fallback(string name)
    {
        switch (name)
        {
            case Boss:
            case Clear:
            case GameOver: return Quest;
            case Quest:    return Field;
            default:       return null;
        }
    }

    private AudioClip Load(string name)
    {
        AudioClip clip;
        if (clips.TryGetValue(name, out clip)) return clip;

        // 無い曲も null で覚えておき、毎フレーム Resources を探しに行かない
        clip = Resources.Load<AudioClip>("Music/" + name);
        clips[name] = clip;
        return clip;
    }

    // ─── 再生・フェード ──────────────────────────────────────────
    private void Switch(string track, AudioClip clip)
    {
        CurrentTrack = track;
        currentClip = clip;

        Channel previous = active;
        if (previous != null) previous.wanted = false;   // 前の曲はフェードアウト
        active = null;
        if (clip == null) return;                        // 曲が無ければ無音へ

        // 前の曲とは別のチャンネルで新しい曲をフェードインさせる（クロスフェード）
        Channel next = previous == channels[0] ? channels[1] : channels[0];

        AudioSource source = next.source;
        source.Stop();
        source.clip = clip;
        source.loop = !track.StartsWith(JinglePrefix);
        source.time = 0f;
        next.level = 0f;
        source.volume = 0f;
        source.Play();

        next.wanted = true;
        active = next;
    }

    private void TickFades()
    {
        float step = Time.unscaledDeltaTime / fadeSeconds;
        float master = Volume;

        foreach (Channel c in channels)
        {
            c.level = Mathf.MoveTowards(c.level, c.wanted ? 1f : 0f, step);
            c.source.volume = c.level * master;

            if (!c.wanted && c.level <= 0f && c.source.isPlaying)
            {
                c.source.Stop();
            }
        }
    }
}
