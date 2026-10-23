using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【診断用】冒険クエスト入口 UI の再生成と、Play での一連の動作確認を自動で行う。
/// Logs/QuestEntryProbe/build.txt     … 未保存のシーンが無ければ QuestProductionUIBuilder.BuildAll() を実行
/// Logs/QuestEntryProbe/entrytest.txt … Play で 常設ボタン→未解放ダイアログ→10体撃破で自動表示→冒険に出る→
///                                        クエスト中はボタン非表示→中断で復帰→Play再開後も解放維持 を検証し撮影する
/// テスト中に書き換えた PlayerPrefs（解放状態）は終了時に元へ戻す。
/// </summary>
[InitializeOnLoad]
public static class QuestEntryProbe
{
    private const string Dir = "Logs/QuestEntryProbe";
    private const string K = "QuestEntryProbe.";
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
    };

    private static readonly Func<bool>[] Actions =
    {
        Prepare, EnterPlay, WaitReady, CheckInitial,
        OpenAndCheckGuard, CheckLockedDialog, ShotsLocked, CloseDialog,
        KillUntil9, CheckAfter9Open, CheckAfter9Close,
        KillTenth, CheckAutoOpened, ShotsUnlocked, CloseDialog,
        KillEleventh, OpenUnlocked, PressAdventure, CheckQuestStarted, AbortQuest, CheckAfterAbort,
        ExitPlay, WaitEditMode, EnterPlay, WaitReady, CheckPersisted, ExitPlay, Finish,
    };

    static QuestEntryProbe()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "ready.txt"), $"{DateTime.Now:o}\tindex={Index}");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ─── 状態（Play 切替のドメインリロードをまたぐため SessionState に置く） ───
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
            if (TakeRequest("build.txt")) { RunBuild(); return; }
            if (TakeRequest("entrytest.txt"))
            {
                File.Delete(Path.Combine(Dir, "result.tsv"));
                foreach (string f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);
                Log("INFO\t開始");
                Index = 0; Sub = 0; Wait(0);
                SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            }
            return;
        }

        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(K + "StepStart", 0f) > 120)
        {
            Check($"タイムアウト(step {Index})", false);
            // 一度だけ記録して Finish へ。経過時間を戻さないと毎フレーム再判定されて止まらなくなる
            SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            if (Index == Actions.Length - 1) { Index = -1; return; }
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

    private static bool TakeRequest(string name)
    {
        string p = Path.Combine(Dir, name);
        if (!File.Exists(p)) return false;
        File.Delete(p);
        return true;
    }

    // ─── Builder 再生成 ─────────────────────────────────────────
    private static void RunBuild()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                File.WriteAllText(Path.Combine(Dir, "build_status.txt"), $"SKIPPED\t未保存のシーンがあるため実行しませんでした: {SceneManager.GetSceneAt(i).path}");
                return;
            }
        }
        try
        {
            QuestProductionUIBuilder.BuildAll();
            File.WriteAllText(Path.Combine(Dir, "build_status.txt"), $"DONE\t{DateTime.Now:o}");
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(Dir, "build_status.txt"), $"ERROR\t{e}");
        }
    }

    // ─── テスト手順 ─────────────────────────────────────────────
    private static bool Prepare()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != "Assets/Scenes/Game.unity")
        {
            if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Index = Actions.Length - 1; return false; }
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        }
        // 解放状態を退避してから未解放の状態でテストする
        SessionState.SetBool(K + "HadUnlocked", PlayerPrefs.HasKey(QuestEntryController.UnlockedKey));
        SessionState.SetInt(K + "Unlocked", PlayerPrefs.GetInt(QuestEntryController.UnlockedKey, 0));
        SessionState.SetBool(K + "HadCount", PlayerPrefs.HasKey(QuestEntryController.DefeatCountKey));
        SessionState.SetInt(K + "Count", PlayerPrefs.GetInt(QuestEntryController.DefeatCountKey, 0));
        PlayerPrefs.DeleteKey(QuestEntryController.UnlockedKey);
        PlayerPrefs.DeleteKey(QuestEntryController.DefeatCountKey);
        PlayerPrefs.Save();
        SessionState.SetInt(K + "OrigSize", UILayoutDiagnostics.GameViewSizeTool.CurrentIndex());
        SetResolution(0);
        return true;
    }

    private static bool EnterPlay()
    {
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        return true;
    }

    private static bool WaitReady()
    {
        if (!EditorApplication.isPlaying || Entry() == null || UnityEngine.Object.FindFirstObjectByType<Idle.GameManager>() == null || !StageProbeSupport.EnsureBattle()) return false;
        if (Sub == 0) { Sub = 1; Wait(2.5); return false; }
        return true;
    }

    private static bool CheckInitial()
    {
        QuestEntryController e = Entry();
        Check("ダイアログは閉じている", !e.IsDialogOpen);
        bool oldCard = FindAll<RectTransform>().Any(r => r.name == "QuestStartCard");
        Check("旧・大型パネルが存在しない", !oldCard);
        bool ownButton = FindAll<RectTransform>().Any(r => r.name == "QuestEntryButton");
        Check("入口ボタンは HUD 側に任せ、Quest 側に常設ボタンを作らない", !ownButton);
        RecordLayout("初期表示 1080x1920");
        return true;
    }

    private static bool OpenAndCheckGuard()
    {
        QuestEntryController e = Entry();
        e.Open(); // HUD の「⚔ 冒険」ボタンと同じ呼び出し
        Check("Open() でダイアログが開く", e.IsDialogOpen);
        Check("開いた直後は誤タップ防止で操作不可", e.dialogGroup != null && !e.dialogGroup.interactable);
        Wait(0.9);
        return true;
    }

    private static bool CheckLockedDialog()
    {
        QuestEntryController e = Entry();
        Check("誤タップ防止が解除される", e.dialogGroup.interactable);
        CheckLocked(e, 0);
        return true;
    }

    private static void CheckLocked(QuestEntryController e, int count)
    {
        Check($"未解放: 見出し『冒険クエスト解放まで』", e.headlineText.text == "冒険クエスト解放まで", e.headlineText.text);
        Check($"未解放: 進捗『敵 {count} / 10体』", e.progressText.gameObject.activeInHierarchy && e.progressText.text == $"敵 {count} / 10体", e.progressText.text);
        Check("未解放: 『閉じる』のみ表示", e.closeButton.gameObject.activeInHierarchy && !e.adventureButton.gameObject.activeInHierarchy);
    }

    private static void CheckUnlocked(QuestEntryController e)
    {
        Check("解放済: 見出し『冒険クエストが解放されました！』", e.headlineText.text == "冒険クエストが解放されました！", e.headlineText.text);
        Check("解放済: 『冒険に出る』『閉じる』の2ボタン", e.adventureButton.gameObject.activeInHierarchy && e.closeButton.gameObject.activeInHierarchy);
        Check("解放済: 進捗表示は隠れる", !e.progressText.gameObject.activeInHierarchy);
    }

    private static bool ShotsLocked() => Shots("locked");
    private static bool ShotsUnlocked() => Shots("unlocked");

    /// <summary>Sub を 0..(解像度数*2) で進め、各解像度で 切替→待機→記録・撮影 を行う</summary>
    private static bool Shots(string tag)
    {
        int i = Sub / 2;
        if (i >= Resolutions.Length) { SetResolution(0); return true; }
        if (Sub % 2 == 0) { SetResolution(i); Sub = Sub + 1; return false; }
        RecordLayout($"{tag} {Label(i)}");
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{tag}_{Label(i)}.png"));
        Wait(1.0);
        Sub = Sub + 1;
        return false;
    }

    private static bool CloseDialog()
    {
        QuestEntryController e = Entry();
        e.closeButton.onClick.Invoke();
        Check("『閉じる』でダイアログが閉じる", !e.IsDialogOpen);
        Wait(0.5);
        return true;
    }

    private static bool KillUntil9()
    {
        if (Sub >= 9) return true;
        if (!KillCurrentEnemy()) return false;
        Sub = Sub + 1;
        Wait(1.4); // 次の敵は 1 秒後に出現する
        return false;
    }

    private static bool CheckAfter9Open()
    {
        QuestEntryController e = Entry();
        Check("9体撃破ではダイアログは自動表示されない", !e.IsDialogOpen);
        Check("9体撃破では未解放のまま", !QuestEntryController.IsUnlocked && QuestEntryController.DefeatCount == 9, $"count={QuestEntryController.DefeatCount}");
        e.Open();
        Wait(0.9);
        return true;
    }

    private static bool CheckAfter9Close()
    {
        QuestEntryController e = Entry();
        CheckLocked(e, 9);
        e.closeButton.onClick.Invoke();
        Wait(0.5);
        return true;
    }

    private static bool KillTenth()
    {
        if (!KillCurrentEnemy()) return false;
        QuestEntryController e = Entry();
        Check("10体目の撃破と同時にダイアログが自動表示される", e.IsDialogOpen);
        Check("解放状態が保存される", QuestEntryController.IsUnlocked);
        Check("自動表示直後も誤タップ防止が効く", e.dialogGroup != null && !e.dialogGroup.interactable);
        Wait(0.9);
        return true;
    }

    private static bool CheckAutoOpened()
    {
        CheckUnlocked(Entry());
        return true;
    }

    private static bool KillEleventh()
    {
        if (Sub == 0)
        {
            if (!KillCurrentEnemy()) return false;
            Sub = 1;
            Wait(1.4);
            return false;
        }
        Check("解放後の撃破では再び自動表示されない", !Entry().IsDialogOpen);
        return true;
    }

    private static bool OpenUnlocked()
    {
        QuestEntryController e = Entry();
        if (Sub == 0) { e.Open(); Sub = 1; Wait(0.9); return false; }
        Check("解放後はいつでも開ける", e.IsDialogOpen);
        CheckUnlocked(e);
        return true;
    }

    private static bool PressAdventure()
    {
        Entry().adventureButton.onClick.Invoke();
        Wait(2.0);
        return true;
    }

    private static bool CheckQuestStarted()
    {
        QuestEntryController e = Entry();
        QuestOverlayController overlay = UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>();
        Check("『冒険に出る』でクエストが始まる", QuestManager.IsQuestActive);
        Check("クエスト開始時にダイアログは閉じる", !e.IsDialogOpen);
        Check("クエスト中は本編（HUD の冒険ボタンを含む）が隠れる", MainCanvas() != null && !MainCanvas().activeInHierarchy);
        Check("クエスト画面が表示される", overlay != null && overlay.questRoot != null && overlay.questRoot.activeInHierarchy);
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "quest_1080x1920.png"));
        Wait(1.0);
        return true;
    }

    private static bool AbortQuest()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.AbortQuest();
        Wait(1.0);
        return true;
    }

    private static bool CheckAfterAbort()
    {
        QuestEntryController e = Entry();
        Check("クエスト終了で本編に戻る", !QuestManager.IsQuestActive);
        Check("クエスト終了後に本編（HUD）が戻る", MainCanvas() != null && MainCanvas().activeInHierarchy);
        Check("クエスト終了後もダイアログは閉じたまま", !e.IsDialogOpen);
        return true;
    }

    private static bool ExitPlay()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        return true;
    }

    private static bool WaitEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (Sub == 0) { Sub = 1; Wait(1.0); return false; }
        return true;
    }

    private static bool CheckPersisted()
    {
        QuestEntryController e = Entry();
        if (Sub == 0)
        {
            Check("Play し直しても解放状態が残る", QuestEntryController.IsUnlocked);
            Check("解放済みでも起動時には自動表示しない", !e.IsDialogOpen);
            e.Open();
            Sub = 1;
            Wait(0.9);
            return false;
        }
        CheckUnlocked(e);
        e.closeButton.onClick.Invoke();
        return true;
    }

    private static bool Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return false; }
        RestorePrefs();
        int orig = SessionState.GetInt(K + "OrigSize", -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        int fail = lines.Count(l => l.Contains("\tFAIL\t"));
        int pass = lines.Count(l => l.Contains("\tPASS\t"));
        Log($"INFO\t完了 PASS={pass} FAIL={fail}");
        return true;
    }

    private static void RestorePrefs()
    {
        if (SessionState.GetBool(K + "HadUnlocked", false)) PlayerPrefs.SetInt(QuestEntryController.UnlockedKey, SessionState.GetInt(K + "Unlocked", 0));
        else PlayerPrefs.DeleteKey(QuestEntryController.UnlockedKey);
        if (SessionState.GetBool(K + "HadCount", false)) PlayerPrefs.SetInt(QuestEntryController.DefeatCountKey, SessionState.GetInt(K + "Count", 0));
        else PlayerPrefs.DeleteKey(QuestEntryController.DefeatCountKey);
        PlayerPrefs.Save();
    }

    // ─── 補助 ───────────────────────────────────────────────────
    private static QuestEntryController Entry() => FindAll<QuestEntryController>().FirstOrDefault();

    private static GameObject MainCanvas()
    {
        Canvas c = FindAll<Canvas>().FirstOrDefault(x => x.isRootCanvas && x.name == "Canvas");
        return c != null ? c.gameObject : null;
    }

    /// <summary>現在の敵を本来の撃破処理（TakeDamage → Die → OnEnemyDefeated）で倒す。倒せる敵がいなければ false</summary>
    private static bool KillCurrentEnemy()
    {
        int last = SessionState.GetInt(K + "LastKilled", 0);
        Idle.EnemyController enemy = UnityEngine.Object.FindObjectsByType<Idle.EnemyController>(FindObjectsSortMode.None)
            .FirstOrDefault(x => x != null && x.GetInstanceID() != last && x.GetCurrentHp() > 0);
        if (enemy == null) return false;
        SessionState.SetInt(K + "LastKilled", enemy.GetInstanceID());
        enemy.TakeDamage(1000000);
        return true;
    }

    private static void RecordLayout(string stage)
    {
        QuestEntryController e = Entry();
        Canvas questCanvas = e.GetComponentInParent<Canvas>(true).rootCanvas;
        Rect px = questCanvas.pixelRect;
        RectTransform hud = FindAll<RectTransform>().FirstOrDefault(r => r.name == "StatusWindow");
        RectTransform card = FindAll<RectTransform>().FirstOrDefault(r => r.name == "QuestDialogCard");
        RectTransform dim = FindAll<RectTransform>().FirstOrDefault(r => r.name == "DimOverlay");
        Log($"LAYOUT\t{stage}\tview={px.width}x{px.height}\thud={(hud != null ? Fmt(UILayoutProbeRuntime.ScreenRect(hud)) : "-")}\tdialogCard={(card != null && card.gameObject.activeInHierarchy ? Fmt(UILayoutProbeRuntime.ScreenRect(card)) : "(閉)")}");
        bool inside(Rect r) => r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= px.width + 0.5f && r.yMax <= px.height + 0.5f;
        if (dim != null && dim.gameObject.activeInHierarchy)
        {
            Rect d = UILayoutProbeRuntime.ScreenRect(dim);
            Check($"{stage}: 暗幕が画面全体を覆う", d.xMin <= 0.5f && d.yMin <= 0.5f && d.xMax >= px.width - 0.5f && d.yMax >= px.height - 0.5f, Fmt(d));
        }
        if (card != null && card.gameObject.activeInHierarchy)
        {
            Rect c = UILayoutProbeRuntime.ScreenRect(card);
            Check($"{stage}: ダイアログが画面内", inside(c), Fmt(c));
            Check($"{stage}: ダイアログが中央", Mathf.Abs(c.center.x - px.width / 2f) < 2f, $"centerX={c.center.x:0} / {px.width / 2f:0}");
        }
    }

    private static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";
    private static string Label(int i) => $"{Resolutions[i].x}x{Resolutions[i].y}";

    private static void SetResolution(int i)
    {
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(Resolutions[i].x, Resolutions[i].y, $"UI診断 {Label(i)}");
        Wait(1.5);
    }

    private static System.Collections.Generic.List<T> FindAll<T>() where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return list;
    }
}
