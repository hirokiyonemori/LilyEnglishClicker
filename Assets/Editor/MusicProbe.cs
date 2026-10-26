using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【診断用】MusicDirector が場面に合わせて曲を切り替えるかを Play で確認する。
/// Logs/MusicProbe/test.txt を置くと開始。結果は result.tsv。
/// 本物の本編曲（bgm_field）は実物を使い、クエスト・ボス・クリアは「無音の仮の曲」を登録して切り替えだけを確認する
/// （確認中にテスト音が鳴らないようにするため）。シーンは保存しない。
/// </summary>
[InitializeOnLoad]
public static class MusicProbe
{
    private const string Dir = "Logs/MusicProbe";
    private const string K = "MusicProbe.";

    private sealed class Case
    {
        public string Label;
        public QuestState State;
        public string ExpectTrack;
        public bool? ExpectLoop;
        public Action<MusicDirector> Setup;
    }

    private static readonly Case[] Cases =
    {
        new Case { Label = "1_クエスト中は bgm_quest",           State = QuestState.ShowingChoice, ExpectTrack = "bgm_quest",     ExpectLoop = true,
                   Setup = d => { d.RegisterClip("bgm_quest", Silent("q")); d.RegisterClip("bgm_boss", Silent("b")); d.RegisterClip("jingle_clear", Silent("c")); } },
        new Case { Label = "2_ボス戦は bgm_boss",               State = QuestState.BossBattle,    ExpectTrack = "bgm_boss",      ExpectLoop = true },
        new Case { Label = "3_クリアは jingle_clear（ループなし）", State = QuestState.QuestClear,     ExpectTrack = "jingle_clear",  ExpectLoop = false },
        new Case { Label = "4_ボス曲が無ければ bgm_quest で代用",  State = QuestState.BossBattle,    ExpectTrack = "bgm_quest",     ExpectLoop = true,
                   Setup = d => d.UnregisterClip("bgm_boss") },
        new Case { Label = "5_クエスト外に戻ると bgm_field",      State = QuestState.Idle,          ExpectTrack = "bgm_field",     ExpectLoop = true },
        new Case { Label = "6_クエスト曲も無ければ bgm_field で代用", State = QuestState.ShowingChoice, ExpectTrack = "bgm_field",     ExpectLoop = true,
                   Setup = d => d.UnregisterClip("bgm_quest") },
    };

    static MusicProbe()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "ready.txt"), $"{DateTime.Now:o}\tindex={Index}");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static int Index { get => SessionState.GetInt(K + "Index", -1); set => SessionState.SetInt(K + "Index", value); }
    private static int Sub { get => SessionState.GetInt(K + "Sub", 0); set => SessionState.SetInt(K + "Sub", value); }
    private static void Wait(double s) => SessionState.SetFloat(K + "WaitUntil", (float)(EditorApplication.timeSinceStartup + s));
    private static bool Waiting => EditorApplication.timeSinceStartup < SessionState.GetFloat(K + "WaitUntil", 0f);
    private static double nextPoll;

    private static void Log(string line) => File.AppendAllText(Path.Combine(Dir, "result.tsv"), $"{DateTime.Now:HH:mm:ss}\t{line}\n");
    private static void Check(string name, bool ok, string detail = "") => Log($"{(ok ? "PASS" : "FAIL")}\t{name}\t{detail}");

    private static void Tick()
    {
        if (Index < 0)
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string p = Path.Combine(Dir, "test.txt");
            if (!File.Exists(p)) return;
            File.Delete(p);
            File.Delete(Path.Combine(Dir, "result.tsv"));
            Log("INFO\t開始");
            Index = 0; Sub = 0; Wait(0);
            SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            return;
        }

        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(K + "StepStart", 0f) > 120)
        {
            Check($"タイムアウト(step {Index})", false);
            Finish();
            return;
        }
        if (Waiting) return;

        try { Step(); }
        catch (Exception e) { Check($"例外(step {Index})", false, e.ToString().Replace('\n', ' ')); Finish(); }
    }

    // Index: 0=準備 1=Play開始 2=準備待ち 3=本編曲 4..=各ケース 末尾=Play終了
    private static void Step()
    {
        if (Index == 0)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != "Assets/Scenes/Game.unity")
            {
                if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Finish(); return; }
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            }
            // 取り込み設定（Streaming など）を確実に当てるため、曲を再取り込みする
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Music" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Wait(1.0); Next(); return;
        }
        if (Index == 1) { if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true; Next(); return; }
        if (Index == 2)
        {
            if (!EditorApplication.isPlaying || MusicDirector.Instance == null || QuestManager.Instance == null) return;
            if (Sub == 0) { Sub = 1; Wait(3.0); return; }   // フェードインが終わるまで待つ
            Next(); return;
        }
        if (Index == 3)
        {
            MusicDirector d = MusicDirector.Instance;
            AudioClip real = Resources.Load<AudioClip>("Music/bgm_field");
            Check("本編曲 bgm_field.ogg を Resources から読める", real != null, real != null ? $"{real.length:0.0}s {real.channels}ch {real.frequency}Hz" : "null");
            Check("本編: 流れている曲は bgm_field", d.CurrentTrack == "bgm_field", d.CurrentTrack ?? "(無音)");
            Check("本編: ループ再生", d.IsLooping);
            Check("本編: 音量が設定値に達している", Mathf.Abs(d.CurrentOutputVolume - MusicDirector.Volume) < 0.02f, $"{d.CurrentOutputVolume:0.00} / {MusicDirector.Volume:0.00}");
            Check("本編: 実際に再生中", real != null && d.GetComponents<AudioSource>().Any(s => s.isPlaying && s.clip == real));
            Next(); return;
        }
        int ci = Index - 4;
        if (ci < Cases.Length)
        {
            Case c = Cases[ci];
            MusicDirector d = MusicDirector.Instance;
            if (Sub == 0)
            {
                if (c.Setup != null) c.Setup(d);
                SetProp(QuestManager.Instance, "CurrentState", c.State);
                Sub = 1; Wait(1.8); return;   // クロスフェード（1秒）が終わるまで待つ
            }
            Check($"{c.Label}", d.CurrentTrack == c.ExpectTrack, d.CurrentTrack ?? "(無音)");
            if (c.ExpectLoop.HasValue) Check($"{c.Label}: ループ = {c.ExpectLoop}", d.IsLooping == c.ExpectLoop.Value);
            Check($"{c.Label}: 音量が出ている", d.CurrentOutputVolume > MusicDirector.Volume * 0.9f, $"{d.CurrentOutputVolume:0.00}");
            Next(); return;
        }
        if (Index == 4 + Cases.Length) { if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; Next(); return; }
        Finish();
    }

    private static void Next()
    {
        Sub = 0;
        Index = Index + 1;
        SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
    }

    private static void Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; Wait(1.0); return; }
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={lines.Count(l => l.Contains("\tFAIL\t"))}");
        Index = -1;
    }

    private static AudioClip Silent(string name)
    {
        // 確認中にテスト音が鳴らないよう、無音の短いクリップを使う
        AudioClip clip = AudioClip.Create(name, 44100 * 2, 1, 44100, false);
        clip.SetData(new float[44100 * 2], 0);
        return clip;
    }

    private static void SetProp(object target, string name, object value)
    {
        PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p.GetSetMethod(true).Invoke(target, new[] { value });
    }
}
