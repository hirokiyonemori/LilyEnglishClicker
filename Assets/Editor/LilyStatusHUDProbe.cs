using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【診断用】下部 Lily ステータス HUD の生成と、Play での 4 状態の検証を自動で行う。
/// Logs/LilyStatusHUDProbe/build.txt … 未保存のシーンが無ければ LilyStatusHUDBuilder で StatusWindow を再構築して保存
/// Logs/LilyStatusHUDProbe/test.txt  … Play で ①通常 ②10体未満で冒険ボタン ③10体達成 ④解放後に再度冒険ボタン
///                                     を確認し、各状態を 4 解像度で撮影する（結果は result.tsv）
/// テスト中に書き換えた PlayerPrefs（解放状態・進捗）は終了時に元へ戻す。
/// </summary>
[InitializeOnLoad]
public static class LilyStatusHUDProbe
{
    private const string Dir = "Logs/LilyStatusHUDProbe";
    private const string K = "LilyStatusHUDProbe.";
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
    };

    private static readonly Func<bool>[] Actions =
    {
        Prepare, EnterPlay, WaitReady,
        CheckNormal, ShotsNormal,
        KillUntil6, CheckProgress6, PressHudLocked, CheckLockedDialog, ShotsLocked, CloseDialog,
        KillUntil9, KillTenth, CheckUnlockedMoment, ShotsUnlocked, CloseDialog,
        PressHudUnlocked, CheckReopened, ShotsReopen, PressAdventure, CheckQuestStarted, AbortQuest, CheckAfterQuest,
        CheckTapAttack, ExitPlay, Finish,
    };

    static LilyStatusHUDProbe()
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
            if (TakeRequest("build.txt")) { RunBuild(); return; }
            if (TakeRequest("dirtycopy.txt"))
            {
                // 未保存の変更内容を調べるため、開いている Game シーンを別名コピーとして書き出す（元ファイルは変更しない）
                Scene scene = SceneManager.GetActiveScene();
                bool ok = EditorSceneManager.SaveScene(scene, "Temp/LilyStatusHUDProbe_dirty.unity", true);
                File.WriteAllText(Path.Combine(Dir, "dirtycopy_status.txt"), $"{(ok ? "DONE" : "ERROR")}\t{scene.path}\tdirty={scene.isDirty}\t{DateTime.Now:o}");
                return;
            }
            if (TakeRequest("test.txt"))
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

    // ─── HUD 生成 ───────────────────────────────────────────────
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
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Game.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            LilyStatusHUDBuilder.Build(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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
        if (!EditorApplication.isPlaying || Entry() == null || Hud() == null || GM() == null || !StageProbeSupport.EnsureBattle()) return false;
        if (Sub == 0) { Sub = 1; Wait(2.5); return false; }
        return true;
    }

    // ① 通常時
    private static bool CheckNormal()
    {
        QuestHudButton hud = Hud();
        Check("① HUD の冒険ボタンが表示・操作可能", hud.gameObject.activeInHierarchy && hud.GetComponent<Button>().interactable);
        Check("① 名前 LILY", Text("NameText") == "LILY", Text("NameText"));
        Check("① レベル Lv.1", Text("LevelText") == "Lv.1", Text("LevelText"));
        int max = QuestManager.Instance != null ? QuestManager.Instance.BasePlayerMaxHp : -1;
        Check("① HP 数値が QuestManager の最大HPと一致", Text("HpText") == $"{max} / {max}", $"{Text("HpText")} (BasePlayerMaxHp={max})");
        Slider gauge = InHud<Slider>("HpGauge");
        Check("① HP ゲージが満タン", gauge != null && Mathf.Approximately(gauge.value, 1f), gauge != null ? gauge.value.ToString("0.00") : "-");
        Check("① 進捗 0 / 10 を表示", hud.progressRoot.activeInHierarchy && hud.progressText.text == "0 / 10", hud.progressText.text);
        Check("① 未解放なので ! バッジは非表示", !hud.unlockedBadge.gameObject.activeInHierarchy);
        Check("① ダイアログは閉じている", !Entry().IsDialogOpen);
        Check("① 旧ステータスの文字（HP300 / Lv1）が残っていない",
              !FindAll<Text>().Any(t => t.text == "HP300" || t.text == "Lv1"));
        Check("① EXP バーを追加していない", !FindAll<RectTransform>().Any(r => r.name.IndexOf("Exp", StringComparison.OrdinalIgnoreCase) >= 0 && r.IsChildOf(Window())));
        Check("① 大型 Quest パネルが常時表示されていない", !FindAll<RectTransform>().Any(r => (r.name == "QuestStartCard" || r.name == "QuestDialogCard") && r.gameObject.activeInHierarchy));
        CheckRaycast("①");
        return true;
    }

    // ② 10体未満で冒険ボタン
    private static bool KillUntil6() => KillUntil(6);

    private static bool CheckProgress6()
    {
        QuestHudButton hud = Hud();
        Check("② 撃破数（GameManager.currentScore）が増える", GM().currentScore == 6, $"score={GM().currentScore}");
        Check("② ゴールドが加算される（既存の報酬処理）", GM().currentGold > 0, $"gold={GM().currentGold}");
        Check("② HUD の進捗が 6 / 10", hud.progressText.text == "6 / 10", hud.progressText.text);
        Check("② 6体ではダイアログは自動表示されない", !Entry().IsDialogOpen);
        return true;
    }

    private static bool PressHudLocked()
    {
        Hud().GetComponent<Button>().onClick.Invoke();
        Wait(1.0);
        return true;
    }

    private static bool CheckLockedDialog()
    {
        QuestEntryController e = Entry();
        Check("② 冒険ボタンでダイアログが開く（未解放でも押せる）", e.IsDialogOpen);
        string progress = e.progressText != null ? e.progressText.text : "";
        string headline = e.headlineText != null ? e.headlineText.text : "";
        Check("② 『冒険クエスト解放まで　敵 6 / 10体』", headline.Contains("冒険クエスト解放まで") && progress.Contains("6 / 10"), $"{headline} / {progress}");
        Check("② 『閉じる』のみ", e.closeButton.gameObject.activeInHierarchy && !e.adventureButton.gameObject.activeInHierarchy);
        return true;
    }

    private static bool CloseDialog()
    {
        Entry().closeButton.onClick.Invoke();
        Check("『閉じる』でダイアログが閉じる", !Entry().IsDialogOpen);
        Wait(0.5);
        return true;
    }

    // ③ 10体達成
    private static bool KillUntil9() => KillUntil(9);

    private static bool KillTenth()
    {
        if (Sub == 0)
        {
            Check("③ 9体では未解放のまま", !QuestEntryController.IsUnlocked && !Entry().IsDialogOpen);
            if (!KillCurrentEnemy()) return false;
            Sub = 1;
            Wait(1.0);
            return false;
        }
        return true;
    }

    private static bool CheckUnlockedMoment()
    {
        QuestHudButton hud = Hud();
        QuestEntryController e = Entry();
        Check("③ 10体目で解放ダイアログが自動表示", e.IsDialogOpen && QuestEntryController.IsUnlocked);
        Check("③ 『冒険に出る』『閉じる』を表示", e.adventureButton.gameObject.activeInHierarchy && e.closeButton.gameObject.activeInHierarchy);
        Check("③ HUD の進捗表示が消える", !hud.progressRoot.activeInHierarchy);
        Check("③ HUD に ! バッジが出る", hud.unlockedBadge.gameObject.activeInHierarchy);
        Check("③ HUD ボタンが金色になる", hud.face.topColor == hud.unlockedTop);
        Check("③ 撃破数は 10", GM().currentScore == 10, $"score={GM().currentScore}");
        return true;
    }

    // ④ 解放後に再度冒険ボタン
    private static bool PressHudUnlocked()
    {
        Hud().GetComponent<Button>().onClick.Invoke();
        Wait(1.0);
        return true;
    }

    private static bool CheckReopened()
    {
        QuestEntryController e = Entry();
        Check("④ 解放後も冒険ボタンからダイアログが開く", e.IsDialogOpen);
        Check("④ 『冒険に出る』『閉じる』を表示", e.adventureButton.gameObject.activeInHierarchy && e.closeButton.gameObject.activeInHierarchy);
        return true;
    }

    private static bool PressAdventure()
    {
        Entry().adventureButton.onClick.Invoke();
        Wait(1.5);
        return true;
    }

    private static bool CheckQuestStarted()
    {
        Check("④ 『冒険に出る』でクエスト開始", QuestManager.IsQuestActive);
        Check("④ クエスト中は HUD が隠れる", !Hud().gameObject.activeInHierarchy);
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "4_quest_1080x1920.png"));
        Wait(1.0);
        return true;
    }

    private static bool AbortQuest()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.AbortQuest();
        Wait(1.0);
        return true;
    }

    private static bool CheckAfterQuest()
    {
        QuestHudButton hud = Hud();
        Check("④ クエスト終了で HUD が戻る", hud.gameObject.activeInHierarchy && !QuestManager.IsQuestActive);
        Check("④ 戻った後も ! バッジ（解放済み）", hud.unlockedBadge.gameObject.activeInHierarchy && !hud.progressRoot.activeInHierarchy);
        int max = QuestManager.Instance.BasePlayerMaxHp;
        Check("④ クエスト後の HP 表示が最大HPに戻る", Text("HpText") == $"{max} / {max}", Text("HpText"));
        Check("④ 撃破数はクエストで変化しない", GM().currentScore == 10, $"score={GM().currentScore}");
        return true;
    }

    /// <summary>HUD の余白（ボタン以外）をタップした時は従来どおり本編の攻撃になることを確認する</summary>
    private static bool CheckTapAttack()
    {
        if (Sub == 0)
        {
            SessionState.SetInt(K + "ScoreBefore", GM().currentScore);
            GM().Click();
            StatusFace(out Sprite now, out Sprite attack);
            Check("本編タップで顔アイコンが攻撃表情に変わる", attack == null || now == attack);
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "5_attack_face_1080x1920.png"));
            Sub = 1;
            Wait(0.6);
            return false;
        }
        StatusFace(out Sprite idleNow, out _);
        Check("表情は一定時間後に戻る", idleNow != null);
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
        RestorePrefs();
        int orig = SessionState.GetInt(K + "OrigSize", -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={lines.Count(l => l.Contains("\tFAIL\t"))}");
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

    // ─── 撮影とレイアウト確認 ───────────────────────────────────
    private static bool ShotsNormal() => Shots("1_normal");
    private static bool ShotsLocked() => Shots("2_locked");
    private static bool ShotsUnlocked() => Shots("3_unlocked");
    private static bool ShotsReopen() => Shots("4_reopen");

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

    private static void RecordLayout(string stage)
    {
        RectTransform window = Window();
        Canvas canvas = window.GetComponentInParent<Canvas>().rootCanvas;
        Rect view = canvas.pixelRect;
        float scale = canvas.scaleFactor;
        Rect hud = UILayoutProbeRuntime.ScreenRect(window);
        Rect button = UILayoutProbeRuntime.ScreenRect((RectTransform)Hud().transform);
        Rect badge = UILayoutProbeRuntime.ScreenRect(Hud().unlockedBadge);
        Rect lily = ScreenRectOf("Player");
        Rect enemy = EnemyRect();
        Rect gold = ScreenRectOf("Text_Gold_BG");
        Log($"LAYOUT\t{stage}\tview={view.width}x{view.height}\tscale={scale:0.###}\thud={Fmt(hud)}\tbutton={Fmt(button)}\tlily={Fmt(lily)}\tenemy={Fmt(enemy)}");

        bool Inside(Rect r) => r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= view.width + 0.5f && r.yMax <= view.height + 0.5f;
        Check($"{stage}: HUD が画面内", Inside(hud), Fmt(hud));
        Check($"{stage}: 冒険ボタンが画面内", Inside(button), Fmt(button));
        if (Hud().unlockedBadge.gameObject.activeInHierarchy) Check($"{stage}: ! バッジが画面内", Inside(badge), Fmt(badge));
        Check($"{stage}: HUD の高さは 150（参照解像度換算）以下", hud.height / scale <= 150.5f, $"{hud.height / scale:0.#}");
        Check($"{stage}: HUD が Lily と重ならない", !hud.Overlaps(lily), $"hudTop={hud.yMax:0} lilyBottom={lily.yMin:0}");
        if (enemy.width > 0f) Check($"{stage}: HUD が敵と重ならない", !hud.Overlaps(enemy), $"hudTop={hud.yMax:0} enemyBottom={enemy.yMin:0}");
        if (gold.width > 0f) Check($"{stage}: 上部 GOLD 表示が画面内に残っている", Inside(gold) && Find<RectTransform>("Text_Gold_BG").gameObject.activeInHierarchy, Fmt(gold));
        foreach (TMP_Text t in window.GetComponentsInChildren<TMP_Text>(false))
        {
            // Overflow 設定の文字が枠からはみ出していないか（描画上の幅で判定）
            t.ForceMeshUpdate();
            Check($"{stage}: 文字『{t.text}』が枠に収まる", t.preferredWidth <= t.rectTransform.rect.width + 1f || t.GetComponentInParent<LayoutGroup>() != null,
                  $"{t.preferredWidth:0}/{t.rectTransform.rect.width:0}");
        }
    }

    private static void CheckRaycast(string stage)
    {
        EventSystem es = EventSystem.current;
        if (es == null) { Check($"{stage} EventSystem が無い", false); return; }
        Rect button = UILayoutProbeRuntime.ScreenRect((RectTransform)Hud().transform);
        var hits = new List<RaycastResult>();
        es.RaycastAll(new PointerEventData(es) { position = button.center }, hits);
        string top = hits.Count > 0 ? hits[0].gameObject.name : "(なし)";
        Check($"{stage} 冒険ボタンの位置のタップがボタンに届く（ClickOn に塞がれない）", hits.Count > 0 && hits[0].gameObject.GetComponentInParent<QuestHudButton>() != null, top);

        Rect hud = UILayoutProbeRuntime.ScreenRect(Window());
        Vector2 panelPoint = new Vector2(hud.xMin + hud.width * 0.45f, hud.center.y);
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = panelPoint }, hits);
        top = hits.Count > 0 ? hits[0].gameObject.name : "(なし)";
        Check($"{stage} HUD の余白タップは従来どおり本編の攻撃（ClickOn）へ届く", top == "ClickOn", top);
    }

    // ─── 補助 ───────────────────────────────────────────────────
    private static bool KillUntil(int target)
    {
        if (GM().currentScore >= target) { Wait(0.3); return true; }
        if (!KillCurrentEnemy()) return false;
        Wait(1.4); // 次の敵は 1 秒後に出現する
        return false;
    }

    /// <summary>現在の敵を本来の撃破処理（TakeDamage → Die → OnEnemyDefeated）で倒す</summary>
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

    private static void StatusFace(out Sprite now, out Sprite attack)
    {
        Idle.StatusWindowController c = FindAll<Idle.StatusWindowController>().FirstOrDefault();
        var so = new SerializedObject(c);
        Image face = so.FindProperty("faceIcon").objectReferenceValue as Image;
        now = face != null ? face.sprite : null;
        attack = so.FindProperty("faceAttack").objectReferenceValue as Sprite;
    }

    private static Rect EnemyRect()
    {
        Idle.EnemyController enemy = UnityEngine.Object.FindObjectsByType<Idle.EnemyController>(FindObjectsSortMode.None).FirstOrDefault();
        if (enemy == null) return new Rect();
        Image image = enemy.GetComponentsInChildren<Image>(false).OrderByDescending(i => i.rectTransform.rect.width * i.rectTransform.rect.height).FirstOrDefault();
        return image != null ? UILayoutProbeRuntime.ScreenRect(image.rectTransform) : UILayoutProbeRuntime.ScreenRect((RectTransform)enemy.transform);
    }

    private static Rect ScreenRectOf(string name)
    {
        RectTransform r = Find<RectTransform>(name);
        return r != null ? UILayoutProbeRuntime.ScreenRect(r) : new Rect();
    }

    /// <summary>HUD 内の文字を読む。敵 HUD など同名のオブジェクトを拾わないよう StatusWindow 配下に限定する</summary>
    private static string Text(string name)
    {
        TMP_Text t = InHud<TMP_Text>(name);
        return t != null ? t.text : "(なし)";
    }

    private static T InHud<T>(string name) where T : Component =>
        Window().GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);

    private static T Find<T>(string name) where T : Component => FindAll<T>().FirstOrDefault(c => c.name == name);
    private static QuestEntryController Entry() => FindAll<QuestEntryController>().FirstOrDefault();
    private static QuestHudButton Hud() => FindAll<QuestHudButton>().FirstOrDefault();
    private static Idle.GameManager GM() => UnityEngine.Object.FindFirstObjectByType<Idle.GameManager>();
    private static RectTransform Window() => (RectTransform)FindAll<Idle.StatusWindowController>().First().transform;

    private static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";
    private static string Label(int i) => $"{Resolutions[i].x}x{Resolutions[i].y}";

    private static void SetResolution(int i)
    {
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(Resolutions[i].x, Resolutions[i].y, $"UI診断 {Label(i)}");
        Wait(1.5);
    }

    private static List<T> FindAll<T>() where T : Component
    {
        var list = new List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return list;
    }
}
