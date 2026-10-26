using System;
using System.IO;
using System.Linq;
using Idle;
using LilyEnglish.Growth;
using LilyEnglish.Growth.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ステージ制（拠点→ステージ選択→戦闘→ボス→クリア→拠点→強化→次のステージ）の自動検証。
/// Logs/StageProbe/test.txt を置くと Play で実行し、結果を result.tsv と PNG に残す。
/// batchmode: Unity -batchmode -projectPath . -executeMethod StageProbe.RunBatch（-quit は付けない）。
/// セーブ（data.bin・強化データ・ステージ進行）は退避して、終了時に元へ戻す。
/// </summary>
[InitializeOnLoad]
public static class StageProbe
{
    private const string Dir = "Logs/StageProbe";
    private const string K = "StageProbe.";
    private const string GrowthSaveKey = "lily_growth_save_v1";
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
        new Vector2Int(1536, 2048), // iPad 相当（縦横比 3:4）
    };
    private static readonly Func<bool>[] Actions =
    {
        Prepare, EnterPlay, WaitBase, CheckBase, ShotsBase, TryLockedStage,
        StartStage1, CheckBattle1, ShotsBattle, KillNormals, CheckBoss, KillBoss, WaitResult, CheckResult, ShotsResult,
        NoAutoAdvance, ReturnToBase, CheckBaseAfterClear, ShotsBaseAfterClear,
        Enhance, StartStage2, KillOneInStage2, RetreatFlow, RetryFlow, QuestFlow, LaterStages, ExitPlay, Finish,
    };
    private static double nextPoll;

    static StageProbe()
    {
        Directory.CreateDirectory(Dir);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static int Index { get => SessionState.GetInt(K + "Index", -1); set => SessionState.SetInt(K + "Index", value); }
    private static int Sub { get => SessionState.GetInt(K + "Sub", 0); set => SessionState.SetInt(K + "Sub", value); }
    private static void Wait(double s) => SessionState.SetFloat(K + "WaitUntil", (float)(EditorApplication.timeSinceStartup + s));
    private static bool Waiting => EditorApplication.timeSinceStartup < SessionState.GetFloat(K + "WaitUntil", 0f);
    private static void Log(string line) => File.AppendAllText(Path.Combine(Dir, "result.tsv"), $"{DateTime.Now:HH:mm:ss}\t{line}\n");
    private static void Check(string name, bool ok, string detail = "") => Log($"{(ok ? "PASS" : "FAIL")}\t{name}\t{detail}");
    private static string DataBinPath => Path.Combine(Application.persistentDataPath, "data.bin");
    private static string BackupPath => Path.Combine(Dir, "data.bin.backup");
    private static long Long(string key) => long.Parse(SessionState.GetString(K + key, "0"));
    private static void Remember(string key, long value) => SessionState.SetString(K + key, value.ToString());

    private static void Tick()
    {
        if (Index < 0)
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (Take("quit.txt")) { EditorApplication.Exit(0); return; }
            if (Take("test.txt"))
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
            Check($"タイムアウト(step {Index} {Actions[Index].Method.Name})", false);
            SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            if (Index == Actions.Length - 1) { Index = -1; return; }
            Index = Actions.Length - 1;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            return;
        }
        if (Waiting) return;
        bool done;
        try { done = Actions[Index](); }
        catch (Exception e) { Check($"例外(step {Index} {Actions[Index].Method.Name})", false, e.ToString().Replace('\n', ' ')); done = true; }
        if (!done) return;
        Sub = 0;
        Index = Index + 1;
        SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
        if (Index >= Actions.Length) Index = -1;
    }

    private static bool Take(string name)
    {
        string p = Path.Combine(Dir, name);
        if (!File.Exists(p)) return false;
        File.Delete(p);
        return true;
    }

    // ─── 手順 ───────────────────────────────────────────────────
    private static bool Prepare()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != StageUIBuilder.ScenePath)
        {
            if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Index = Actions.Length - 1; return false; }
            EditorSceneManager.OpenScene(StageUIBuilder.ScenePath);
        }
        SessionState.SetBool(K + "HadDataBin", File.Exists(DataBinPath));
        if (File.Exists(DataBinPath)) File.Copy(DataBinPath, BackupPath, true);
        SessionState.SetBool(K + "HadGrowth", PlayerPrefs.HasKey(GrowthSaveKey));
        SessionState.SetString(K + "Growth", PlayerPrefs.GetString(GrowthSaveKey, ""));
        SessionState.SetBool(K + "HadStage", PlayerPrefs.HasKey(StageProgress.SaveKey));
        SessionState.SetString(K + "Stage", PlayerPrefs.GetString(StageProgress.SaveKey, ""));
        SessionState.SetInt(K + "QuestUnlocked", PlayerPrefs.GetInt(QuestEntryController.UnlockedKey, 0));
        SessionState.SetInt(K + "QuestDefeats", PlayerPrefs.GetInt(QuestEntryController.DefeatCountKey, 0));
        PlayerPrefs.SetInt(QuestEntryController.UnlockedKey, 1);
        // 強化とステージ進行は新規の状態から始める（所持金 data.bin はそのまま使う）
        PlayerPrefs.DeleteKey(GrowthSaveKey);
        StageProgress.ResetAll();
        return true;
    }

    private static bool EnterPlay()
    {
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        return true;
    }

    private static bool WaitBase()
    {
        if (!EditorApplication.isPlaying || Director() == null || GrowthService.Instance == null || Director().CurrentPhase != StageDirector.Phase.Base) return false;
        if (Sub == 0) { Sub = 1; Wait(1.5); return false; }
        return true;
    }

    private static bool CheckBase()
    {
        StageDirector d = Director();
        Idle.GameManager gm = Game();
        DataManager.data.MoneyPerSecond = 0; // 放置収入が混ざらないようにして、ゴールドの加算を厳密に数える
        Check("StageDirector が GameManager と結び付いている", gm != null && gm.stageDirector == d);
        Check("起動すると拠点から始まる", d.CurrentPhase == StageDirector.Phase.Base && StageProgress.Active == 0);
        StageSelectView view = Select();
        Check("ステージ選択画面が出ている", view.gameObject.activeInHierarchy);
        Check("戦闘中の表示（ステージ名・退却）は隠れている", Find<RectTransform>("BattleHud") != null && !Find<RectTransform>("BattleHud").gameObject.activeSelf);
        Check("敵が出ていない", Enemy() == null);
        Check("ステージ一覧がカタログの件数ぶんある", view.Cards.Count == StageCatalog.Count && StageCatalog.Count == 5, $"{view.Cards.Count}/{StageCatalog.Count}");
        for (int n = 1; n <= StageCatalog.Count; n++)
        {
            StageCardUI card = view.Cards[n - 1];
            bool lockOn = CardChild(n, "ThumbFrame/LockOverlay").gameObject.activeSelf;
            bool button = card.ChallengeButton.gameObject.activeSelf;
            Check($"ステージ{n}: {(n == 1 ? "解放済み（挑戦できる）" : "未解放（鍵・選べない）")}", n == 1 ? !lockOn && button : lockOn && !button, $"lock={lockOn} button={button}");
            Sprite art = CardChild(n, "ThumbFrame/Thumbnail").GetComponent<Image>().sprite;
            Check($"ステージ{n}: サムネイルが背景画像", art != null && art.name == StageCatalog.Get(n).background, art != null ? art.name : "(なし)");
            Check($"ステージ{n}: ステージ名・推奨レベルの表示", Text(CardChild(n, "Info/NameText")) == StageCatalog.Get(n).displayName && Text(CardChild(n, "Info/RecommendText")).StartsWith($"推奨 Lv.{StageCatalog.Get(n).recommendedLevel}"), $"{Text(CardChild(n, "Info/NameText"))} / {Text(CardChild(n, "Info/RecommendText"))}");
        }
        Check("ステージ1は未クリアの表示", !CardChild(1, "Info/ClearBadge").gameObject.activeSelf);
        Check("リリィのレベル・ゴールド・EXP の表示", Text(Find<RectTransform>("LevelText", Select().transform)) == "リリィ Lv.1" && Text(Find<RectTransform>("Gold", Select().transform).Find("Value")) == DataManager.data.Money.ToString("N0"),
            $"{Text(Find<RectTransform>("LevelText", Select().transform))} / {Text(Find<RectTransform>("Gold", Select().transform).Find("Value"))}");
        // 文言：主人公はリリィ本人なので「仲間」ではない
        string allLines = string.Join("\n", DefaultVillagerScenarios.Get().SelectMany(sc => sc.lines).Select(l => l.text));
        Check("村人のセリフに「リリィの仲間」が無い（師匠は「リリィじゃないか」）", !allLines.Contains("仲間") && allLines.Contains("リリィじゃないか"));
        string shopText = string.Join("\n", GrowthService.Instance.Upgrades.Select(u => u.description));
        Check("強化ショップの説明に「仲間」が無い（リリィ本人の強化）", !shopText.Contains("仲間"), shopText.Replace("\n", " / "));
        long money = DataManager.data.Money;
        gm.Click();
        Check("拠点では画面タップでゴールドが増えない", DataManager.data.Money == money, $"{money}→{DataManager.data.Money}");
        return true;
    }

    private static bool ShotsBase() => Shots("base", VerifySelect);

    private static bool TryLockedStage()
    {
        StageDirector d = Director();
        bool started = d.StartStage(2);
        Check("未解放のステージ2は始められない", !started && d.CurrentPhase == StageDirector.Phase.Base && StageProgress.Active == 0);
        return true;
    }

    private static bool StartStage1()
    {
        if (Sub == 0)
        {
            GrowthService gs = GrowthService.Instance;
            Remember("MoneyBefore", DataManager.data.Money);
            Remember("ExpBefore", gs.GetBalance(Currency.Exp));
            Remember("LevelBefore", gs.MainLevel);
            SessionState.SetFloat(K + "PowerBefore", gs.AttackPowerExact);
            Card(1).ChallengeButton.onClick.Invoke();
            Sub = 1; Wait(1.8);
            return false;
        }
        return true;
    }

    private static bool CheckBattle1()
    {
        StageDirector d = Director();
        Idle.GameManager gm = Game();
        Check("カードの「挑戦」で戦闘が始まる", d.CurrentPhase == StageDirector.Phase.Battle && StageProgress.Active == 1);
        Check("拠点の画面は閉じる・戦闘中の表示が出る", !Select().gameObject.activeSelf && Find<RectTransform>("BattleHud").gameObject.activeSelf);
        Image bg = Background();
        Check("背景がステージ1の画像", bg != null && bg.sprite != null && bg.sprite.name == StageCatalog.Get(1).background, bg != null && bg.sprite != null ? bg.sprite.name : "-");
        Check("ステージ名の表示", Text(Find<RectTransform>("StageNameText", Find<RectTransform>("StagePlate").transform)) == "STAGE 1　ひだまりの森", Text(Find<RectTransform>("StageNameText", Find<RectTransform>("StagePlate").transform)));
        Check("開始バナーが出る", Find<CanvasGroup>("StageBanner").gameObject.activeInHierarchy && Text(Find<RectTransform>("BannerTitleText")) == "STAGE 1", Text(Find<RectTransform>("BannerTitleText")));
        EnemyController e = Enemy();
        Check("敵が出ている", e != null);
        if (e != null)
        {
            EnemyData data = e.GetEnemyData();
            Check("ステージ1の敵HPは 1.0 倍", e.GetMaxHp() == Mathf.Max(1, data.baseHp), $"{e.GetMaxHp()} / base {data.baseHp}");
            Check("序盤はティア1の敵", data.tier == 1, $"tier {data.tier}");
        }
        Check("ステージの集計が 0 から始まる", gm.currentScore == 0 && gm.currentGold == 0);
        Check("最後に選んだステージが保存される", StageProgress.LastSelected == 1 && PlayerPrefs.GetString(StageProgress.SaveKey).Contains("\"lastSelected\":1"), PlayerPrefs.GetString(StageProgress.SaveKey));
        Check("ステージ開始でゴールドは変わらない", DataManager.data.Money == Long("MoneyBefore"));
        return true;
    }

    private static bool ShotsBattle() => Shots("battle", VerifyBattle);

    private static bool KillNormals()
    {
        Idle.GameManager gm = Game();
        if (Sub == 0) { SessionState.SetBool(K + "EarlyBoss", false); SessionState.SetInt(K + "MaxTier", 0); Sub = 1; }
        if (gm.BossPhase) return Enemy() != null;
        EnemyController e = Enemy();
        if (e != null)
        {
            EnemyData data = e.GetEnemyData();
            if (data != null)
            {
                if (data.tier >= 5) SessionState.SetBool(K + "EarlyBoss", true);
                SessionState.SetInt(K + "MaxTier", Mathf.Max(SessionState.GetInt(K + "MaxTier", 0), data.tier));
            }
            e.TakeDamage(1000000);
        }
        return false;
    }

    private static bool CheckBoss()
    {
        Idle.GameManager gm = Game();
        GrowthService gs = GrowthService.Instance;
        Check("通常の敵を25体倒す（ボスは途中で出ない）", gm.currentScore == 25 && !SessionState.GetBool(K + "EarlyBoss", false), $"score={gm.currentScore}");
        Check("通常の敵はステージ1のティア範囲（1〜3）", SessionState.GetInt(K + "MaxTier", 0) <= 3, $"max tier {SessionState.GetInt(K + "MaxTier", 0)}");
        EnemyController boss = Enemy();
        Check("25体のあとにボスが出る", gm.BossPhase && boss != null && boss.GetEnemyData() != null && boss.GetEnemyData().tier == 5, boss != null && boss.GetEnemyData() != null ? boss.GetEnemyData().displayName : "-");
        if (boss != null && boss.GetEnemyData() != null)
            Check("ボスは通常の敵より強い（HP 180 以上）", boss.GetMaxHp() >= 180 && boss.GetMaxHp() > 90, $"HP {boss.GetMaxHp()}");
        Check("ボス出現のバナー", Text(Find<RectTransform>("BannerTitleText")) == "WARNING!", Text(Find<RectTransform>("BannerTitleText")));
        Check("敵撃破の EXP が入る（25体×10）", gs.StageExpEarned == 250, gs.StageExpEarned.ToString());
        Check("プレートがボス戦の表示", Text(Find<RectTransform>("StageProgressText")) == "BOSS BATTLE!", Text(Find<RectTransform>("StageProgressText")));
        return true;
    }

    private static bool KillBoss()
    {
        if (Sub == 0)
        {
            Enemy().TakeDamage(1000000);
            Sub = 1; Wait(0.4);
            return false;
        }
        StageDirector d = Director();
        Check("ボスを倒すとクリア状態になる（攻撃を受け付けない）", d.CurrentPhase == StageDirector.Phase.Cleared);
        long money = DataManager.data.Money;
        Game().Click();
        Check("クリア待ちのタップでゴールドが増えない", DataManager.data.Money == money);
        return true;
    }

    private static bool WaitResult()
    {
        GameObject panel = ResultPanel();
        if (panel == null || !panel.activeSelf) return false;
        if (Sub == 0) { Sub = 1; Wait(0.5); return false; }
        return true;
    }

    private static bool CheckResult()
    {
        Idle.GameManager gm = Game();
        GrowthService gs = GrowthService.Instance;
        StageResultView result = Result();
        Check("クリア画面が出る", ResultPanel().activeInHierarchy);
        Check("STAGE CLEAR! の表示", Text(Find<RectTransform>("TitleText", result.transform)) == "STAGE CLEAR!");
        Check("クリアしたステージ名の表示", Text(Find<RectTransform>("StageNameText", result.transform)) == "STAGE 1　ひだまりの森", Text(Find<RectTransform>("StageNameText", result.transform)));
        long expected = 25 * 50 + 250; // 通常25体×50G ＋ ボス（50G×5）
        Check("獲得GOLD = 25体＋ボス（ステージ1は等倍）", gm.currentGold == expected, $"{gm.currentGold} (期待 {expected})");
        Check("獲得GOLDの表示が集計と一致", Text(Find<RectTransform>("GoldValue", result.transform)) == $"{gm.currentGold:N0} G", Text(Find<RectTransform>("GoldValue", result.transform)));
        Check("所持金への加算は1回だけ（二重加算なし）", DataManager.data.Money - Long("MoneyBefore") == gm.currentGold, $"+{DataManager.data.Money - Long("MoneyBefore")} / 集計 {gm.currentGold}");
        long expGain = gs.GetBalance(Currency.Exp) - Long("ExpBefore");
        Check("獲得EXP = 26体×10 + クリアボーナス100", gs.StageExpEarned == 360 && expGain == 360, $"表示 {gs.StageExpEarned} / 実際の増加 {expGain}");
        Check("獲得EXPの表示", Text(Find<RectTransform>("ExpValue", result.transform)) == "360 EXP", Text(Find<RectTransform>("ExpValue", result.transform)));
        Check("クリアボーナスの注記", Text(Find<RectTransform>("ExpNoteText", result.transform)).Contains("100"), Text(Find<RectTransform>("ExpNoteText", result.transform)));
        Check("次のステージ解放のお知らせ", Find<RectTransform>("UnlockText", result.transform).gameObject.activeSelf && Text(Find<RectTransform>("UnlockText", result.transform)).Contains("STAGE 2"), Text(Find<RectTransform>("UnlockText", result.transform)));
        Check("「拠点に戻る」「もう一度挑戦」ボタン", result.BaseButton != null && result.RetryButton != null && Text(result.BaseButton.transform.Find("Label")) == "拠点に戻る" && Text(result.RetryButton.transform.Find("Label")) == "もう一度挑戦");
        Check("クリアが記録され、ステージ2が解放される", StageProgress.IsCleared(1) && StageProgress.IsUnlocked(2) && !StageProgress.IsUnlocked(3) && StageProgress.HighestUnlocked == 2);
        Check("進行が保存されている", PlayerPrefs.GetString(StageProgress.SaveKey) == StageProgress.ExportJson() && PlayerPrefs.GetString(StageProgress.SaveKey).Contains("\"cleared\":[1]"), PlayerPrefs.GetString(StageProgress.SaveKey));
        Check("所持金データ（data.bin）が残っている", File.Exists(DataBinPath));
        return true;
    }

    private static bool ShotsResult() => Shots("result", VerifyResult);

    private static bool NoAutoAdvance()
    {
        if (Sub == 0) { Sub = 1; Wait(4.0); return false; }
        StageDirector d = Director();
        GrowthService gs = GrowthService.Instance;
        Check("次のステージへ自動で進まない", d.CurrentPhase == StageDirector.Phase.Cleared && StageProgress.Active == 1 && Enemy() == null && ResultPanel().activeSelf);
        Check("待っても報酬は増えない（二重加算なし）", DataManager.data.Money - Long("MoneyBefore") == Game().currentGold && gs.GetBalance(Currency.Exp) - Long("ExpBefore") == 360,
            $"G+{DataManager.data.Money - Long("MoneyBefore")} EXP+{gs.GetBalance(Currency.Exp) - Long("ExpBefore")}");
        return true;
    }

    private static bool ReturnToBase()
    {
        if (Sub == 0)
        {
            Result().BaseButton.onClick.Invoke();
            Sub = 1; Wait(0.6);
            return false;
        }
        StageDirector d = Director();
        Check("「拠点に戻る」で拠点へ戻る", d.CurrentPhase == StageDirector.Phase.Base && StageProgress.Active == 0 && Select().gameObject.activeSelf && !ResultPanel().activeSelf);
        Check("拠点では本編の既定背景に戻る", Background().sprite != null && Background().sprite.name.StartsWith("Background"), Background().sprite != null ? Background().sprite.name : "-");
        Check("戦闘中の表示は隠れる・敵は出ない", !Find<RectTransform>("BattleHud").gameObject.activeSelf && Enemy() == null);
        return true;
    }

    private static bool CheckBaseAfterClear()
    {
        Check("ステージ1に CLEAR が付き「再挑戦」になる", CardChild(1, "Info/ClearBadge").gameObject.activeSelf && Text(CardChild(1, "Info/ChallengeButton/Label")) == "再挑戦", Text(CardChild(1, "Info/ChallengeButton/Label")));
        Check("ステージ2が解放され NEW! が付く", !CardChild(2, "ThumbFrame/LockOverlay").gameObject.activeSelf && CardChild(2, "Info/ChallengeButton").gameObject.activeSelf && CardChild(2, "Info/NewBadge").gameObject.activeSelf);
        Check("ステージ3はまだ鍵がかかっている", CardChild(3, "ThumbFrame/LockOverlay").gameObject.activeSelf && !CardChild(3, "Info/ChallengeButton").gameObject.activeSelf);
        Check("ゴールドの表示が所持金と一致（シーンを戻っても消えない）", Text(Find<RectTransform>("Gold", Select().transform).Find("Value")) == DataManager.data.Money.ToString("N0"));
        Check("EXP の表示がクリア分を含む", Text(Find<RectTransform>("Exp", Select().transform).Find("Value")) == GrowthService.Instance.GetBalance(Currency.Exp).ToString("N0"), Text(Find<RectTransform>("Exp", Select().transform).Find("Value")));
        return true;
    }

    private static bool ShotsBaseAfterClear() => Shots("base_cleared", VerifySelectFrontier);

    private static bool Enhance()
    {
        GrowthService gs = GrowthService.Instance;
        GrowthUIView ui = GrowthUIView.Find();
        if (Sub == 0)
        {
            Find<RectTransform>("ShopButton", Select().transform).GetComponent<Button>().onClick.Invoke();
            Sub = 1; Wait(0.4);
            return false;
        }
        if (Sub == 1)
        {
            Check("拠点から強化ショップが開く", ui.shopRoot.activeInHierarchy);
            ui.CloseAll();
            Find<RectTransform>("CharacterButton", Select().transform).GetComponent<Button>().onClick.Invoke();
            Sub = 2; Wait(0.4);
            return false;
        }
        if (Sub == 2)
        {
            Check("拠点からキャラ詳細（レベルアップ）が開く", ui.detail.gameObject.activeInHierarchy);
            Check("獲得EXPで強化できる印（!）が出る", Find<RectTransform>("AffordableBadge", Find<RectTransform>("CharacterButton", Select().transform)).gameObject.activeSelf);
            ui.CloseAll();
            float before = gs.AttackPowerExact;
            bool a = gs.TryLevelUp(gs.MainCharacter);
            bool b = gs.TryLevelUp(gs.MainCharacter);
            Check("EXPでリリィがレベルアップできる（Lv1→3）", a && b && gs.MainLevel == 3, $"Lv{gs.MainLevel}");
            Check("攻撃力が上がる（次のステージにも引き継ぐ）", gs.AttackPowerExact > before, $"{before:0.00}→{gs.AttackPowerExact:0.00}");
            Sub = 3; Wait(0.6);
            return false;
        }
        Check("強化後、推奨レベルの表示が更新される（ステージ2は推奨Lv.3＝達成）", Text(CardChild(2, "Info/RecommendText")) == "推奨 Lv.3", Text(CardChild(2, "Info/RecommendText")));
        Check("推奨レベルに届かないステージは「いま Lv.3」が付く", Text(CardChild(3, "Info/RecommendText")) == "推奨 Lv.5（いま Lv.3）", Text(CardChild(3, "Info/RecommendText")));
        Check("リリィのレベル表示が更新される", Text(Find<RectTransform>("LevelText", Select().transform)) == "リリィ Lv.3", Text(Find<RectTransform>("LevelText", Select().transform)));
        return true;
    }

    private static bool StartStage2()
    {
        if (Sub == 0)
        {
            Remember("MoneyBefore", DataManager.data.Money);
            Card(2).ChallengeButton.onClick.Invoke();
            Sub = 1; Wait(1.8);
            return false;
        }
        StageDirector d = Director();
        Check("ステージ2を選んで挑戦できる", d.CurrentPhase == StageDirector.Phase.Battle && StageProgress.Active == 2);
        Image bg = Background();
        Check("背景がステージ2の画像", bg.sprite != null && bg.sprite.name == StageCatalog.Get(2).background, bg.sprite != null ? bg.sprite.name : "-");
        EnemyController e = Enemy();
        Check("強化したレベルが引き継がれている（威力が上がったまま）", GrowthService.Instance.MainLevel == 3 && GrowthService.Instance.AttackPowerExact > 1.2f, GrowthService.Instance.AttackPowerExact.ToString("0.00"));
        if (e != null)
        {
            EnemyData data = e.GetEnemyData();
            int expected = Mathf.Max(1, Mathf.RoundToInt(data.baseHp * 1.5f));
            Check("ステージ2の敵HPは 1.5 倍", e.GetMaxHp() == expected, $"{e.GetMaxHp()} (期待 {expected}, base {data.baseHp})");
        }
        else Check("ステージ2で敵が出る", false);
        Check("ステージ集計がリセットされ、所持金は戻らない", Game().currentScore == 0 && Game().currentGold == 0 && DataManager.data.Money == Long("MoneyBefore"));
        Check("プレートがステージ2の表示", Text(Find<RectTransform>("StageNameText", Find<RectTransform>("StagePlate").transform)) == "STAGE 2　花咲く丘");
        return true;
    }

    private static bool KillOneInStage2()
    {
        if (Sub == 0)
        {
            Enemy().TakeDamage(1000000);
            Sub = 1; Wait(3.5); // コインが所持金へ届くまで待つ
            return false;
        }
        long gain = DataManager.data.Money - Long("MoneyBefore");
        Check("ステージ2の撃破ゴールドは 1.4 倍（50→70）", gain == 70, $"+{gain}");
        return true;
    }

    private static bool RetreatFlow()
    {
        StageDirector d = Director();
        Button retreat = Find<RectTransform>("RetreatButton").GetComponent<Button>();
        if (Sub == 0)
        {
            Remember("MoneyBefore", DataManager.data.Money);
            SessionState.SetString(K + "JsonBefore", StageProgress.ExportJson());
            retreat.onClick.Invoke();
            Sub = 1; Wait(0.3);
            return false;
        }
        if (Sub == 1)
        {
            Check("退却ボタンは1回目では戻らず確認表示になる", d.CurrentPhase == StageDirector.Phase.Battle && Text(retreat.transform.Find("Label")) == "もう一度で退却", Text(retreat.transform.Find("Label")));
            retreat.onClick.Invoke();
            Sub = 2; Wait(0.6);
            return false;
        }
        Check("2回目の退却で拠点へ戻る", d.CurrentPhase == StageDirector.Phase.Base && StageProgress.Active == 0 && Enemy() == null);
        Check("退却してもゴールド・進行は変わらない", DataManager.data.Money == Long("MoneyBefore") && StageProgress.ExportJson() == SessionState.GetString(K + "JsonBefore", ""));
        Check("退却したステージは未クリアのまま", !StageProgress.IsCleared(2));
        return true;
    }

    /// <summary>ステージ1を「もう一度挑戦」：ボスまで早送りしてクリア → 再挑戦ボタン</summary>
    private static bool RetryFlow()
    {
        StageDirector d = Director();
        Idle.GameManager gm = Game();
        if (Sub == 0)
        {
            Remember("ExpBefore", GrowthService.Instance.GetBalance(Currency.Exp));
            Card(1).ChallengeButton.onClick.Invoke();
            Sub = 1; Wait(1.5);
            return false;
        }
        if (Sub == 1)
        {
            gm.currentScore = gm.targetScore - 1; // 次の1体を倒せばボス戦
            Enemy().TakeDamage(1000000);
            Sub = 2; Wait(2.2);
            return false;
        }
        if (Sub == 2)
        {
            Check("（早送り）25体でボスが出る", gm.BossPhase && Enemy() != null && Enemy().GetEnemyData().tier == 5);
            Enemy().TakeDamage(1000000);
            Sub = 3;
            return false;
        }
        if (Sub == 3)
        {
            if (!ResultPanel().activeSelf) return false;
            Check("（再挑戦）2回目のクリアでもクリア画面が出る", Director().CurrentPhase == StageDirector.Phase.Cleared);
            Check("2回目のクリアでは次のステージの解放通知は出ない", !Find<RectTransform>("UnlockText", Result().transform).gameObject.activeSelf || !Text(Find<RectTransform>("UnlockText", Result().transform)).Contains("STAGE 2"));
            Result().RetryButton.onClick.Invoke();
            Sub = 4; Wait(1.8);
            return false;
        }
        Check("「もう一度挑戦」で同じステージを最初から始める", d.CurrentPhase == StageDirector.Phase.Battle && StageProgress.Active == 1 && gm.currentScore == 0 && !gm.BossPhase && !ResultPanel().activeSelf);
        Check("再挑戦で普通の敵から始まる", Enemy() != null && Enemy().GetEnemyData().tier < 5);
        Check("再クリアでもクリアは1件のまま", StageProgress.ClearedCount == 1, StageProgress.ClearedCount.ToString());
        return true;
    }

    /// <summary>
    /// 既存の冒険クエストを壊していないこと：戦闘中に入口→開始→（クエスト中はステージ操作が無視される）→
    /// 道中敵の戦闘で「敵が攻撃してくる」のが見える→ボスの攻撃力→中止→戦闘に戻る
    /// </summary>
    private static bool QuestFlow()
    {
        StageDirector d = Director();
        QuestEntryController entry = All<QuestEntryController>().FirstOrDefault();
        BattlePanel battle = All<BattlePanel>().FirstOrDefault();
        System.Reflection.BindingFlags nonPublic = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        if (Sub == 0)
        {
            Check("（クエスト）戦闘中に HUD の冒険ボタンが使える", entry != null && QuestEntryController.IsUnlocked && Find<QuestHudButton>("AdventureButton") != null);
            Find<QuestHudButton>("AdventureButton").GetComponent<Button>().onClick.Invoke();
            Sub = 1; Wait(0.8);
            return false;
        }
        if (Sub == 1)
        {
            Check("（クエスト）入口のダイアログが開く", entry.IsDialogOpen);
            entry.adventureButton.onClick.Invoke();
            Sub = 2; Wait(1.5);
            return false;
        }
        if (Sub == 2)
        {
            Check("（クエスト）冒険が始まる", global::QuestManager.IsQuestActive);
            Check("（クエスト）クエスト中はステージを始められない", !d.StartStage(2) && StageProgress.Active == 1);
            Find<RectTransform>("RetreatButton").GetComponent<Button>().onClick.Invoke();
            Find<RectTransform>("RetreatButton").GetComponent<Button>().onClick.Invoke();
            Check("（クエスト）クエスト中は退却も無視される", d.CurrentPhase == StageDirector.Phase.Battle);
            // 道中敵の戦闘を直接始める（道の選択は抽選なので、確実に敵と戦うため）
            typeof(global::QuestManager).GetMethod("StartEnemyBattle", nonPublic).Invoke(global::QuestManager.Instance, null);
            Sub = 3; Wait(1.2);
            return false;
        }
        if (Sub == 3)
        {
            int maxHp = battle.PlayerMaxHp;
            int expected = Mathf.Max(1, Mathf.CeilToInt(maxHp * 4 / 100f));
            Check("（敵の攻撃）道中敵の攻撃力が Lily の最大HP（" + maxHp + "）に見合う（4%以上）", battle.EnemyAttack >= expected && battle.EnemyAttack >= 4, $"攻撃力 {battle.EnemyAttack} / 最大HP {maxHp}");
            Check("（敵の攻撃）Lily のターンから始まる", battle.CurrentTurn == BattleTurnState.PlayerTurn, battle.CurrentTurn.ToString());
            Remember("HpBefore", battle.PlayerHp);
            Remember("EnemyAttack", battle.EnemyAttack);
            Check("（敵の攻撃）攻撃コマンドが実行できる", battle.TryAttack());
            Sub = 4;
            return false;
        }
        if (Sub == 4)
        {
            // 敵のターンに入るまで待つ。入ったら、攻撃してくることが文字で分かる
            if (battle.CurrentTurn != BattleTurnState.EnemyTurn) return false;
            Check("（敵の攻撃）敵のターンで「の攻撃！」と表示される", battle.ActionMessage.Contains("の攻撃！"), battle.ActionMessage);
            Sub = 5;
            return false;
        }
        if (Sub == 5)
        {
            // HP が減った瞬間に、ダメージ表示（-N）が出ている
            if (battle.PlayerHp >= Long("HpBefore")) return false;
            long dealt = Long("HpBefore") - battle.PlayerHp;
            Check("（敵の攻撃）Lily が敵の攻撃力ぶんダメージを受ける", dealt == Long("EnemyAttack"), $"-{dealt} / 攻撃力 {Long("EnemyAttack")}");
            Check("（敵の攻撃）「Lily は N ダメージを受けた！」と表示される", battle.ActionMessage.Contains("ダメージを受けた"), battle.ActionMessage);
            RectTransform floatText = Find<RectTransform>("LilyDamageFloat");
            Check("（敵の攻撃）Lily の横にダメージの数字（-N）が出る", floatText != null && floatText.gameObject.activeInHierarchy && Text(floatText).StartsWith("-" + dealt), floatText != null ? Text(floatText) : "(なし)");
            Sub = 6;
            return false;
        }
        if (Sub == 6)
        {
            if (battle.CurrentTurn != BattleTurnState.PlayerTurn) return false;
            Check("（敵の攻撃）反撃のあと Lily のターンに戻る", true);
            Sub = 7; Wait(1.0);
            return false;
        }
        if (Sub == 7)
        {
            RectTransform floatText = Find<RectTransform>("LilyDamageFloat");
            Check("（敵の攻撃）ダメージの数字は自然に消える", floatText == null || !floatText.gameObject.activeSelf);
            Image portrait = Find<RectTransform>("LilyImage") != null ? Find<RectTransform>("LilyImage").GetComponent<Image>() : null;
            Check("（敵の攻撃）Lily の立ち絵の色が元に戻っている", portrait == null || (Mathf.Approximately(portrait.color.r, 1f) && Mathf.Approximately(portrait.color.g, 1f)), portrait != null ? portrait.color.ToString() : "-");
            typeof(global::QuestManager).GetMethod("StartBossBattle", nonPublic).Invoke(global::QuestManager.Instance, null);
            Sub = 8; Wait(1.5);
            return false;
        }
        if (Sub == 8)
        {
            int maxHp = battle.PlayerMaxHp;
            Check("（敵の攻撃）ボスの攻撃力が Lily の最大HPの 8% 以上で、道中敵より強い", battle.EnemyAttack >= Mathf.CeilToInt(maxHp * 8 / 100f) && battle.EnemyAttack > Long("EnemyAttack"), $"ボス {battle.EnemyAttack} / 道中敵 {Long("EnemyAttack")} / 最大HP {maxHp}");
            global::QuestManager.Instance.AbortQuest();
            Sub = 9; Wait(1.2);
            return false;
        }
        Check("（クエスト）中止すると戦闘に戻る", !global::QuestManager.IsQuestActive && d.CurrentPhase == StageDirector.Phase.Battle && Enemy() != null);
        return true;
    }

    /// <summary>ステージ3〜5の背景と、ステージ5クリア後にステージ1へ戻らないこと</summary>
    private static bool LaterStages()
    {
        StageDirector d = Director();
        Idle.GameManager gm = Game();
        if (Sub == 0)
        {
            d.EnterBase();
            for (int n = 1; n <= 4; n++) StageProgress.Complete(n);
            Select().Show();
            Sub = 1; Wait(0.5);
            return false;
        }
        if (Sub == 1)
        {
            Check("ステージ5まで解放できる", StageProgress.HighestUnlocked == 5 && !CardChild(5, "ThumbFrame/LockOverlay").gameObject.activeSelf);
            Card(3).ChallengeButton.onClick.Invoke();
            Sub = 2; Wait(1.5);
            return false;
        }
        if (Sub == 2)
        {
            Check("ステージ3の背景", Background().sprite != null && Background().sprite.name == StageCatalog.Get(3).background, Background().sprite != null ? Background().sprite.name : "-");
            CaptureOne("stage3_1080x1920", 1080, 1920, VerifyBattleQuiet);
            d.EnterBase();
            Card(4).ChallengeButton.onClick.Invoke();
            Sub = 3; Wait(1.5);
            return false;
        }
        if (Sub == 3)
        {
            Check("ステージ4の背景", Background().sprite != null && Background().sprite.name == StageCatalog.Get(4).background, Background().sprite != null ? Background().sprite.name : "-");
            EnemyData data = Enemy().GetEnemyData();
            Check("ステージ4の敵HPは 2.5 倍", Enemy().GetMaxHp() == Mathf.Max(1, Mathf.RoundToInt(data.baseHp * 2.5f)), $"{Enemy().GetMaxHp()} (base {data.baseHp})");
            CaptureOne("stage4_1080x1920", 1080, 1920, VerifyBattleQuiet);
            d.EnterBase();
            Card(5).ChallengeButton.onClick.Invoke();
            Sub = 4; Wait(1.5);
            return false;
        }
        if (Sub == 4)
        {
            Check("ステージ5の背景", Background().sprite != null && Background().sprite.name == StageCatalog.Get(5).background, Background().sprite != null ? Background().sprite.name : "-");
            EnemyData data = Enemy().GetEnemyData();
            Check("ステージ5の敵HPは 3.0 倍", Enemy().GetMaxHp() == Mathf.Max(1, Mathf.RoundToInt(data.baseHp * 3f)), $"{Enemy().GetMaxHp()} (base {data.baseHp})");
            CaptureOne("stage5_1080x1920", 1080, 1920, VerifyBattleQuiet);
            gm.currentScore = gm.targetScore - 1;
            Enemy().TakeDamage(1000000);
            Sub = 5; Wait(2.2);
            return false;
        }
        if (Sub == 5)
        {
            Check("ステージ5のボスが出る", gm.BossPhase && Enemy() != null && Enemy().GetEnemyData().tier == 5);
            Check("ステージ5のボスのHPは 3.0 倍", Enemy().GetMaxHp() == Mathf.RoundToInt(Enemy().GetEnemyData().baseHp * 3f), Enemy().GetMaxHp().ToString());
            Enemy().TakeDamage(1000000);
            Sub = 6;
            return false;
        }
        if (Sub == 6)
        {
            if (!ResultPanel().activeSelf) return false;
            Check("最終ステージには次の解放通知が出ない", !Find<RectTransform>("UnlockText", Result().transform).gameObject.activeSelf);
            Check("ステージ5クリアが記録される", StageProgress.IsCleared(5) && StageProgress.HighestUnlocked == 5);
            CaptureOne("result_stage5_1080x1920", 1080, 1920, VerifyResultQuiet);
            Sub = 7; Wait(4.0);
            return false;
        }
        if (Sub == 7)
        {
            Check("ステージ5クリア後も勝手にステージ1へ戻らない（自動で次を始めない）", d.CurrentPhase == StageDirector.Phase.Cleared && StageProgress.Active == 5 && Enemy() == null);
            Result().BaseButton.onClick.Invoke();
            Sub = 8; Wait(0.8);
            return false;
        }
        Check("拠点に戻ると、挑戦するステージはプレイヤーが選ぶ", d.CurrentPhase == StageDirector.Phase.Base && StageProgress.Active == 0 && Enemy() == null && StageProgress.LastSelected == 5);
        Check("ステージ5に CLEAR が付く", CardChild(5, "Info/ClearBadge").gameObject.activeSelf);
        return true;
    }

    private static bool ExitPlay()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        return true;
    }

    private static bool Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; return false; }
        if (SessionState.GetBool(K + "HadDataBin", false) && File.Exists(BackupPath)) File.Copy(BackupPath, DataBinPath, true);
        else if (!SessionState.GetBool(K + "HadDataBin", false) && File.Exists(DataBinPath)) File.Delete(DataBinPath);
        if (SessionState.GetBool(K + "HadGrowth", false)) PlayerPrefs.SetString(GrowthSaveKey, SessionState.GetString(K + "Growth", ""));
        else PlayerPrefs.DeleteKey(GrowthSaveKey);
        if (SessionState.GetBool(K + "HadStage", false)) PlayerPrefs.SetString(StageProgress.SaveKey, SessionState.GetString(K + "Stage", ""));
        else PlayerPrefs.DeleteKey(StageProgress.SaveKey);
        PlayerPrefs.SetInt(QuestEntryController.UnlockedKey, SessionState.GetInt(K + "QuestUnlocked", 0));
        PlayerPrefs.SetInt(QuestEntryController.DefeatCountKey, SessionState.GetInt(K + "QuestDefeats", 0));
        PlayerPrefs.Save();
        StageProgress.Reload();
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        int fail = lines.Count(l => l.Contains("\tFAIL\t"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={fail}");
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(fail == 0 ? 0 : 1);
        return true;
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "test.txt"), "");
    }

    // ─── 撮影と配置の検証 ───────────────────────────────────────
    private static string Stage => SessionState.GetString(K + "ShotStage", "");

    private static bool Shots(string tag, Action verify)
    {
        foreach (Vector2Int r in Resolutions)
        {
            SessionState.SetString(K + "ShotStage", $"{tag} {r.x}x{r.y}");
            CaptureOne($"{tag}_{r.x}x{r.y}", r.x, r.y, verify);
        }
        return true;
    }

    /// <summary>
    /// 全 Canvas を一時的に Main Camera の Screen Space - Camera にして w x h の RenderTexture へ描き、同じ状態で配置を検証する
    /// （Game View の解像度切り替えに頼らないので、Unity が背面・batchmode でも動く）。
    /// </summary>
    private static void CaptureOne(string name, int w, int h, Action verify)
    {
        SessionState.SetString(K + "ShotStage", name);
        Camera cam = Camera.main;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevTarget = cam.targetTexture;
        var saved = new System.Collections.Generic.List<(Canvas c, RenderMode mode, Camera cam, float plane, int order, CanvasScaler scaler, bool scalerOn)>();
        try
        {
            cam.targetTexture = rt;
            foreach (Canvas c in All<Canvas>().Where(x => x.isRootCanvas))
            {
                CanvasScaler s = c.GetComponent<CanvasScaler>();
                saved.Add((c, c.renderMode, c.worldCamera, c.planeDistance, c.sortingOrder, s, s != null && s.enabled));
                bool overlay = c.renderMode == RenderMode.ScreenSpaceOverlay;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = overlay ? 40f : 100f;
                if (overlay) c.sortingOrder = 2000 + c.sortingOrder;
                if (s != null) s.enabled = false;
                c.scaleFactor = w / 1080f;
            }
            Canvas.ForceUpdateCanvases();
            foreach (LayoutGroup g in All<LayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
            Canvas.ForceUpdateCanvases();
            // 拠点は表示のたびに一覧を中央へ寄せる。解像度が変わるとずれるので、撮影前にもう一度寄せる
            if (Select() != null && Select().gameObject.activeInHierarchy) { Select().ScrollToFrontier(); Canvas.ForceUpdateCanvases(); }
            verify();
            cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        finally
        {
            foreach (var s in saved)
            {
                s.c.renderMode = s.mode; s.c.worldCamera = s.cam; s.c.planeDistance = s.plane; s.c.sortingOrder = s.order;
                if (s.scaler != null) s.scaler.enabled = s.scalerOn;
            }
            cam.targetTexture = prevTarget;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    private static Rect ViewOf(Component c) => c.GetComponentInParent<Canvas>().rootCanvas.pixelRect;
    private static bool Inside(Rect r, Rect view) => r.xMin >= view.xMin - 0.5f && r.xMax <= view.xMax + 0.5f && r.yMin >= view.yMin - 0.5f && r.yMax <= view.yMax + 0.5f;

    /// <summary>拠点：ヘッダ・一覧・下のボタンが重ならず画面に収まり、ボタンが小さすぎない</summary>
    private static void VerifySelect()
    {
        StageSelectView view = Select();
        RectTransform header = Find<RectTransform>("Header", view.transform);
        RectTransform scroll = Find<RectTransform>("StageScroll", view.transform);
        RectTransform viewport = Find<RectTransform>("Viewport", scroll);
        RectTransform bottom = Find<RectTransform>("BottomBar", view.transform);
        RectTransform shop = Find<RectTransform>("ShopButton", view.transform);
        RectTransform hero = Find<RectTransform>("CharacterButton", view.transform);
        Rect screen = ViewOf(view);
        Rect h = UILayoutProbeRuntime.ScreenRect(header), v = UILayoutProbeRuntime.ScreenRect(viewport), b = UILayoutProbeRuntime.ScreenRect(bottom);
        Rect s1 = UILayoutProbeRuntime.ScreenRect(shop), s2 = UILayoutProbeRuntime.ScreenRect(hero);
        Log($"LAYOUT\t{Stage}\tview={screen.width}x{screen.height}\theader={F(h)}\tlist={F(v)}\tbottom={F(b)}");
        Check($"{Stage}: ヘッダ・一覧・下ボタンが画面内", Inside(h, screen) && Inside(v, screen) && Inside(b, screen), $"{F(h)} {F(v)} {F(b)}");
        Check($"{Stage}: 一覧がヘッダ・下ボタンと重ならない", v.yMax <= h.yMin + 0.5f && v.yMin >= b.yMax - 0.5f, $"list {v.yMin:0}-{v.yMax:0} / header↓{h.yMin:0} / bottom↑{b.yMax:0}");
        Check($"{Stage}: 強化ボタン2つが重ならない", s1.xMax <= s2.xMin + 0.5f, $"{s1.xMax:0} ≤ {s2.xMin:0}");
        float minButton = 0.05f * screen.width; // 幅1080で54px 相当より小さくしない
        Check($"{Stage}: ボタンが小さすぎない", s1.height >= minButton && s2.height >= minButton, $"高さ {s1.height:0}px ≥ {minButton:0}px");
        CheckNoOverlapWithDaily(UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("WalletBar", view.transform)), "ウォレット欄");
        CheckNoOverlapWithDaily(UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("LevelText", view.transform)), "リリィのレベル表示");
        StageCardUI card = view.Cards.FirstOrDefault(c => c.Unlocked && Inside(UILayoutProbeRuntime.ScreenRect((RectTransform)c.transform), v));
        Check($"{Stage}: 解放済みのカードが一覧に1枚以上、全体が見える", card != null);
        if (card != null)
        {
            Rect button = UILayoutProbeRuntime.ScreenRect((RectTransform)card.ChallengeButton.transform);
            Check($"{Stage}: 挑戦ボタンが小さすぎない", button.height >= minButton && button.width >= minButton * 2.5f, $"{button.width:0}x{button.height:0}px");
        }
    }

    /// <summary>クリア後：解放されたばかりの先頭ステージのカードが一覧の中に見える位置までスクロールされている</summary>
    private static void VerifySelectFrontier()
    {
        VerifySelect();
        StageSelectView view = Select();
        Rect v = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("Viewport", view.transform));
        int frontier = StageProgress.HighestUnlocked;
        Rect card = UILayoutProbeRuntime.ScreenRect((RectTransform)view.Cards[frontier - 1].transform);
        Check($"{Stage}: 最新のステージ（{frontier}）のカードが一覧の中に見える", card.center.y >= v.yMin && card.center.y <= v.yMax, $"card {F(card)} / list {F(v)}");
    }

    private static void VerifyBattle()
    {
        RectTransform plate = Find<RectTransform>("StagePlate");
        RectTransform retreat = Find<RectTransform>("RetreatButton");
        Rect screen = ViewOf(plate);
        Rect p = UILayoutProbeRuntime.ScreenRect(plate), r = UILayoutProbeRuntime.ScreenRect(retreat);
        Rect gold = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("Text_Gold"));
        Rect score = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("Text_Score_BG"));
        Rect hud = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("HudPanel"));
        Log($"LAYOUT\t{Stage}\tview={screen.width}x{screen.height}\tplate={F(p)}\tretreat={F(r)}\tgold={F(gold)}\tscore={F(score)}");
        Check($"{Stage}: ステージ名・退却ボタンが画面内", Inside(p, screen) && Inside(r, screen), $"{F(p)} {F(r)}");
        Check($"{Stage}: ステージ名が GOLD/SCORE の下で重ならない", p.yMax <= Mathf.Min(gold.yMin, score.yMin) + 0.5f && r.yMax <= score.yMin + 0.5f, $"{p.yMax:0} ≤ {Mathf.Min(gold.yMin, score.yMin):0}");
        Check($"{Stage}: 退却ボタンとステージ名が重ならない", r.xMax <= p.xMin + 0.5f, $"{r.xMax:0} ≤ {p.xMin:0}");
        Check($"{Stage}: ステージ名が下部 HUD と重ならない", p.yMin >= hud.yMax - 0.5f, $"{p.yMin:0} ≥ {hud.yMax:0}");
        Check($"{Stage}: 退却ボタンが小さすぎない", r.height >= 0.045f * screen.width, $"{r.height:0}px");
        CheckNoOverlapWithDaily(p, "ステージ名"); CheckNoOverlapWithDaily(r, "退却ボタン");
    }

    /// <summary>別機能の「今日のボーナス」ボタン（手前の別 Canvas）と重ならないこと。無ければ検査しない</summary>
    private static void CheckNoOverlapWithDaily(Rect target, string what)
    {
        RectTransform daily = Find<RectTransform>("DailyEntry");
        if (daily == null || !daily.gameObject.activeInHierarchy) return;
        Rect d = UILayoutProbeRuntime.ScreenRect(daily);
        Check($"{Stage}: {what}が「今日のボーナス」と重ならない", !target.Overlaps(d), $"{F(target)} / daily {F(d)}");
    }

    private static void VerifyBattleQuiet()
    {
        RectTransform plate = Find<RectTransform>("StagePlate");
        Rect screen = ViewOf(plate);
        Check($"{Stage}: ステージ名が画面内", Inside(UILayoutProbeRuntime.ScreenRect(plate), screen));
    }

    private static void VerifyResult()
    {
        RectTransform card = Find<RectTransform>("Card", Result().transform);
        RectTransform dim = Find<RectTransform>("DimOverlay", Result().transform);
        Rect screen = ViewOf(card);
        Rect c = UILayoutProbeRuntime.ScreenRect(card), d = UILayoutProbeRuntime.ScreenRect(dim);
        Log($"LAYOUT\t{Stage}\tview={screen.width}x{screen.height}\tcard={F(c)}");
        Check($"{Stage}: クリア画面のカードが画面内", Inside(c, screen), F(c));
        Check($"{Stage}: 暗幕が全画面", d.xMin <= 0.5f && d.yMin <= 0.5f && d.xMax >= screen.width - 0.5f && d.yMax >= screen.height - 0.5f, F(d));
        Rect baseBtn = UILayoutProbeRuntime.ScreenRect((RectTransform)Result().BaseButton.transform);
        Rect retryBtn = UILayoutProbeRuntime.ScreenRect((RectTransform)Result().RetryButton.transform);
        Check($"{Stage}: 2つのボタンが重ならず、小さすぎない", baseBtn.yMin >= retryBtn.yMax - 0.5f && baseBtn.height >= 0.05f * screen.width && retryBtn.height >= 0.05f * screen.width, $"{baseBtn.height:0}/{retryBtn.height:0}px");
    }

    private static void VerifyResultQuiet()
    {
        RectTransform card = Find<RectTransform>("Card", Result().transform);
        Check($"{Stage}: クリア画面のカードが画面内", Inside(UILayoutProbeRuntime.ScreenRect(card), ViewOf(card)));
    }

    // ─── 補助 ───────────────────────────────────────────────────
    private static StageDirector Director() => StageDirector.Instance;
    private static Idle.GameManager Game() => All<Idle.GameManager>().FirstOrDefault();
    private static StageSelectView Select() => All<StageSelectView>().FirstOrDefault();
    private static StageResultView Result() => All<StageResultView>().FirstOrDefault();
    private static GameObject ResultPanel() => Result() != null ? Result().gameObject : null;
    private static StageCardUI Card(int n) => Select().Cards[n - 1];
    private static Transform CardChild(int n, string path) => Card(n).transform.Find(path);
    private static EnemyController Enemy() => All<EnemyController>().FirstOrDefault(e => e.GetCurrentHp() > 0);
    private static Image Background() => All<BackgroundScroller>().Select(s => s.GetComponent<Image>()).FirstOrDefault(i => i != null);
    private static T Find<T>(string name) where T : Component => All<T>().FirstOrDefault(c => c.name == name);

    private static T Find<T>(string name, Transform under) where T : Component
        => under.GetComponentsInChildren<T>(true).FirstOrDefault(c => c.name == name);

    private static string Text(Component c)
    {
        if (c == null) return "(なし)";
        TMP_Text t = c.GetComponent<TMP_Text>();
        return t != null ? t.text : "(文字なし)";
    }

    private static string F(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

    private static System.Collections.Generic.List<T> All<T>() where T : Component
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
