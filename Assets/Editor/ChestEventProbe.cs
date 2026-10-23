using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【診断用】宝箱イベントの 4 状態（閉じた／ふつう／はずれ／レア）が、Resources/Chest の画像で表示されるかを Play で確認して撮影する。
/// Logs/ChestEventProbe/test.txt を置くと開始。結果は result.tsv、画像は chest_*.png。
/// 出現率（Inspector の値）は一時的に書き換えるだけで、Play 終了で元に戻る（シーンは保存しない）。
/// </summary>
[InitializeOnLoad]
public static class ChestEventProbe
{
    private const string Dir = "Logs/ChestEventProbe";
    private const string K = "ChestEventProbe.";

    private static readonly Func<bool>[] Actions =
    {
        Prepare, EnterPlay, WaitReady, ShowClosed,
        OpenRoll0, OpenRoll1, OpenRoll2, ExitPlay, Finish,
    };

    private static readonly (string name, int[] weights)[] Rolls =
    {
        ("open_normal", new[] { 100, 0, 0 }),
        ("open_miss",   new[] { 0, 100, 0 }),
        ("open_rare",   new[] { 0, 0, 100 }),
    };

    static ChestEventProbe()
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
            foreach (string f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);
            Log("INFO\t開始");
            Index = 0; Sub = 0; Wait(0);
            SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            return;
        }

        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(K + "StepStart", 0f) > 90)
        {
            Check($"タイムアウト(step {Index})", false);
            Index = Actions.Length - 1;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            return;
        }
        if (Waiting) { UILayoutDiagnostics.GameViewSizeTool.Repaint(); return; }

        bool done;
        try { done = Actions[Index](); }
        catch (Exception e) { Check($"例外(step {Index})", false, e.ToString().Replace('\n', ' ')); done = true; }
        if (!done) return;
        Sub = 0;
        Index = Index + 1;
        SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
        if (Index >= Actions.Length) Index = -1;
    }

    private static bool Prepare()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != "Assets/Scenes/Game.unity")
        {
            if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Index = Actions.Length - 1; return false; }
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        }
        SessionState.SetInt(K + "OrigSize", UILayoutDiagnostics.GameViewSizeTool.CurrentIndex());
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(1080, 1920, "宝箱確認 1080x1920");
        Wait(1.5);
        return true;
    }

    private static bool EnterPlay()
    {
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        return true;
    }

    private static bool WaitReady()
    {
        if (!EditorApplication.isPlaying || Panel() == null || UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>() == null) return false;
        if (Sub == 0) { Sub = 1; Wait(2.5); return false; }
        return true;
    }

    /// <summary>クエスト画面を表示した状態にして、宝箱イベントを直接開く</summary>
    private static void ShowPanel()
    {
        QuestOverlayController overlay = UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>();
        if (overlay.hideWhileQuesting != null)
            foreach (GameObject go in overlay.hideWhileQuesting) if (go != null) go.SetActive(false);
        if (overlay.questRoot != null) overlay.questRoot.SetActive(true);
        Panel().Show(null, _ => { });
    }

    private static bool ShowClosed()
    {
        if (Sub == 0) { ShowPanel(); Sub = 1; Wait(1.0); return false; }
        string sprite = ChestSpriteName();
        Check("閉じた宝箱の画像が表示される", sprite == "closed", sprite);
        Check("代替エンブレムが隠れている", !EmblemVisible());
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "chest_1_closed.png"));
        Wait(1.0);
        return true;
    }

    private static bool OpenRoll0() => OpenRoll(0);
    private static bool OpenRoll1() => OpenRoll(1);
    private static bool OpenRoll2() => OpenRoll(2);

    private static bool OpenRoll(int i)
    {
        var (expected, weights) = Rolls[i];
        if (Sub == 0)
        {
            ShowPanel();
            var so = new SerializedObject(Panel());
            so.FindProperty("normalWeight").intValue = weights[0];
            so.FindProperty("missWeight").intValue = weights[1];
            so.FindProperty("rareWeight").intValue = weights[2];
            so.ApplyModifiedPropertiesWithoutUndo();
            Button open = new SerializedObject(Panel()).FindProperty("openButton").objectReferenceValue as Button;
            open.onClick.Invoke();
            Sub = 1; Wait(3.0);
            return false;
        }
        string sprite = ChestSpriteName();
        Check($"開封後の画像: {expected}", sprite == expected, sprite);
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"chest_{i + 2}_{expected}.png"));
        Wait(1.0);
        return true;
    }

    private static bool ExitPlay()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        return true;
    }

    private static bool Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return false; }
        int orig = SessionState.GetInt(K + "OrigSize", -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={lines.Count(l => l.Contains("\tFAIL\t"))}");
        return true;
    }

    private static ChestEventPanel Panel() => UnityEngine.Object.FindObjectsByType<ChestEventPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    private static Image ChestImage() => new SerializedObject(Panel()).FindProperty("chestImage").objectReferenceValue as Image;

    private static string ChestSpriteName()
    {
        Image image = ChestImage();
        return image != null && image.sprite != null ? image.sprite.name : "(なし)";
    }

    /// <summary>画像が無い時に出る代替エンブレム（ChestImageSlot/FallbackMedallion）が見えているか</summary>
    private static bool EmblemVisible()
    {
        Image image = ChestImage();
        if (image == null || image.transform.parent == null) return false;
        Transform fallback = image.transform.parent.Find("FallbackMedallion");
        return fallback != null && fallback.gameObject.activeSelf;
    }
}
