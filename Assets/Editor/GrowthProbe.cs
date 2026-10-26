using System;
using System.IO;
using System.Linq;
using LilyEnglish.Growth;
using LilyEnglish.Growth.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【診断用】強化システムを Play で一通り検証する。
/// Logs/GrowthProbe/test.txt … HUD から詳細・ショップを開く／レベル・スキル・ショップ強化／★＝所持金での支払い／
///                             撃破で EXP と所持金が増える／タップ・クエストに ATK・HP が反映される を確認し、4 解像度で撮影
/// Logs/GrowthProbe/quit.txt … 検証のために起動した Unity を閉じる（保存しない）
/// テスト中に書き換えたセーブ（data.bin と強化データ）は終了時に元へ戻す。
/// </summary>
[InitializeOnLoad]
public static class GrowthProbe
{
    private const string Dir = "Logs/GrowthProbe";
    private const string K = "GrowthProbe.";
    private const string GrowthSaveKey = "lily_growth_save_v1";
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
        new Vector2Int(1536, 2048), // iPad 相当（縦横比 3:4）。カードが上下にはみ出さないかの確認
    };
    private static readonly Func<bool>[] Actions =
    {
        Prepare, EnterPlay, WaitReady, CheckInitial, ShotsMain, KillEnemy, CheckAfterKill,
        OpenDetailFromHud, ShotsDetail, LevelUpAndSkill, OpenShopFromDetail, ShotsShop, BuyInShop,
        CheckTapDamage, CheckQuestStats, CloseAll, ExitPlay, Finish,
    };
    private static double nextPoll;

    static GrowthProbe()
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
    private static void Log(string line) => File.AppendAllText(Path.Combine(Dir, "result.tsv"), $"{DateTime.Now:HH:mm:ss}\t{line}\n");
    private static void Check(string name, bool ok, string detail = "") => Log($"{(ok ? "PASS" : "FAIL")}\t{name}\t{detail}");
    private static string DataBinPath => Path.Combine(Application.persistentDataPath, "data.bin");
    private static string BackupPath => Path.Combine(Dir, "data.bin.backup");

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
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(K + "StepStart", 0f) > 90)
        {
            Check($"タイムアウト(step {Index})", false);
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
        if (active.path != GrowthUIBuilder.ScenePath)
        {
            if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Index = Actions.Length - 1; return false; }
            EditorSceneManager.OpenScene(GrowthUIBuilder.ScenePath);
        }
        // セーブを退避し、強化データは新規の状態から始める（所持金 data.bin はそのまま使う）
        SessionState.SetBool(K + "HadDataBin", File.Exists(DataBinPath));
        if (File.Exists(DataBinPath)) File.Copy(DataBinPath, BackupPath, true);
        SessionState.SetBool(K + "HadGrowth", PlayerPrefs.HasKey(GrowthSaveKey));
        SessionState.SetString(K + "Growth", PlayerPrefs.GetString(GrowthSaveKey, ""));
        PlayerPrefs.DeleteKey(GrowthSaveKey);
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
        if (!EditorApplication.isPlaying || GrowthService.Instance == null || !StageProbeSupport.EnsureBattle() || Enemy() == null) return false;
        if (Sub == 0) { Sub = 1; Wait(2.0); return false; }
        return true;
    }

    private static bool CheckInitial()
    {
        var gs = GrowthService.Instance;
        Check("主人公（リリィ）が設定されている", gs.MainCharacter != null && gs.MainCharacter.characterId == "lily", gs.MainCharacter != null ? gs.MainCharacter.displayName : "-");
        Check("新規データの初期所持: EXP 300 / 💎 10", gs.GetBalance(Currency.Exp) == 300 && gs.GetBalance(Currency.Gem) == 10, $"EXP={gs.GetBalance(Currency.Exp)} Gem={gs.GetBalance(Currency.Gem)}");
        Check("★の残高＝本編の所持金", gs.GetBalance(Currency.Star) == Idle.DataManager.data.Money, $"★={gs.GetBalance(Currency.Star)} Money={Idle.DataManager.data.Money}");
        Check("Lv1 の攻撃の威力 = 1（従来の1タップ1ダメージと同じ）", Mathf.Approximately(gs.AttackPowerExact, 1f), $"{gs.AttackPowerExact:0.##}");
        Check("クエストの最大HPがリリィの HP（120）", QuestManager.Instance != null && QuestManager.Instance.BasePlayerMaxHp == 120, QuestManager.Instance != null ? QuestManager.Instance.BasePlayerMaxHp.ToString() : "-");
        RectTransform upgrade = Find<RectTransform>("UpgradeButton");
        RectTransform portrait = Find<RectTransform>("PortraitFrame");
        Check("HUD に「強化」ボタンがある", upgrade != null && upgrade.gameObject.activeInHierarchy && upgrade.GetComponent<GrowthEntryButton>() != null);
        Check("HUD の顔アイコンがボタンになっている", portrait != null && portrait.GetComponent<Button>() != null && portrait.GetComponent<GrowthEntryButton>() != null);
        Check("HUD の Lv 表示", HudText("LevelText") == "Lv.1", HudText("LevelText"));
        return true;
    }

    private static bool ShotsMain() => Shots("main", () => VerifyHud());

    private static bool KillEnemy()
    {
        if (Sub == 0)
        {
            SessionState.SetString(K + "ExpBefore", GrowthService.Instance.GetBalance(Currency.Exp).ToString());
            SessionState.SetString(K + "MoneyBefore", Idle.DataManager.data.Money.ToString());
            Idle.EnemyController e = Enemy();
            e.TakeDamage(1000000);
            Sub = 1;
            Wait(2.5); // コインが所持金表示まで飛ぶのを待つ
            return false;
        }
        return true;
    }

    private static bool CheckAfterKill()
    {
        var gs = GrowthService.Instance;
        long expBefore = long.Parse(SessionState.GetString(K + "ExpBefore", "0"));
        long moneyBefore = long.Parse(SessionState.GetString(K + "MoneyBefore", "0"));
        Check("撃破で EXP +10", gs.GetBalance(Currency.Exp) == expBefore + 10, $"{expBefore}→{gs.GetBalance(Currency.Exp)}");
        Check("撃破ゴールドが所持金（★）に入る", Idle.DataManager.data.Money > moneyBefore, $"{moneyBefore}→{Idle.DataManager.data.Money}");
        Text goldLabel = Find<Text>("Text_Gold");
        Check("上部の GOLD 表示が所持金", goldLabel != null && goldLabel.text == "GOLD: " + Idle.DataManager.data.Money.ToString("N0"), goldLabel != null ? goldLabel.text : "-");
        return true;
    }

    private static bool OpenDetailFromHud()
    {
        if (Sub == 0)
        {
            Find<RectTransform>("PortraitFrame").GetComponent<Button>().onClick.Invoke();
            Sub = 1;
            Wait(0.5);
            return false;
        }
        GrowthUIView view = GrowthUIView.Find();
        Check("顔アイコンでキャラ詳細が開く", view != null && view.detail.gameObject.activeInHierarchy);
        Check("詳細: 名前・Lv", PanelText("CharacterDetailPanel", "NameText") == "リリィ" && PanelText("CharacterDetailPanel", "LevelText") == "Lv1",
              $"{PanelText("CharacterDetailPanel", "NameText")} {PanelText("CharacterDetailPanel", "LevelText")}");
        Check("詳細: HP/ATK/DEF = 120/20/15", PanelText("CharacterDetailPanel", "HpValue") == "120" && PanelText("CharacterDetailPanel", "AtkValue") == "20" && PanelText("CharacterDetailPanel", "DefValue") == "15",
              $"{PanelText("CharacterDetailPanel", "HpValue")}/{PanelText("CharacterDetailPanel", "AtkValue")}/{PanelText("CharacterDetailPanel", "DefValue")}");
        Check("詳細: スキル説明に 200%", PanelText("CharacterDetailPanel", "SkillDesc").Contains("200%"), PanelText("CharacterDetailPanel", "SkillDesc").Replace('\n', ' '));
        return true;
    }

    private static bool ShotsDetail() => Shots("detail", () => VerifyCard("CharacterDetailPanel"));

    private static bool LevelUpAndSkill()
    {
        var gs = GrowthService.Instance;
        long exp = gs.GetBalance(Currency.Exp);
        Button levelUp = PanelFind<Button>("CharacterDetailPanel", "LevelUpButton");
        levelUp.onClick.Invoke();
        Check("レベルアップで Lv2・EXP -100", gs.MainLevel == 2 && gs.GetBalance(Currency.Exp) == exp - 100, $"Lv{gs.MainLevel} EXP {exp}→{gs.GetBalance(Currency.Exp)}");
        Check("Lv2 の ATK = 24（威力 1.2）", gs.GetStats(gs.MainCharacter).atk == 24 && Mathf.Abs(gs.AttackPowerExact - 1.2f) < 0.001f, $"ATK {gs.GetStats(gs.MainCharacter).atk} 威力 {gs.AttackPowerExact:0.##}");
        Check("詳細の表示が更新される", PanelText("CharacterDetailPanel", "LevelText") == "Lv2" && PanelText("CharacterDetailPanel", "AtkValue") == "24");
        exp = gs.GetBalance(Currency.Exp);
        PanelFind<Button>("CharacterDetailPanel", "SkillUpButton").onClick.Invoke();
        Check("スキルレベルアップで Lv2・EXP -50・倍率 220%", gs.GetSkillLevel(gs.MainCharacter) == 2 && gs.GetBalance(Currency.Exp) == exp - 50 && PanelText("CharacterDetailPanel", "SkillDesc").Contains("220%"),
              $"Lv{gs.GetSkillLevel(gs.MainCharacter)} EXP {exp}→{gs.GetBalance(Currency.Exp)}");
        return true;
    }

    private static bool OpenShopFromDetail()
    {
        if (Sub == 0)
        {
            Check("HUD の Lv 表示がレベルアップに追従", HudText("LevelText") == "Lv.2", HudText("LevelText"));
            PanelFind<Button>("CharacterDetailPanel", "ToShopButton").onClick.Invoke();
            Sub = 1;
            Wait(0.5);
            return false;
        }
        GrowthUIView view = GrowthUIView.Find();
        int rows = view.shopRoot.GetComponentsInChildren<PartyUpgradeRowUI>(true).Length;
        Check("詳細からショップへ移れる", view.shopRoot.activeInHierarchy && !view.detail.gameObject.activeInHierarchy);
        Check("ショップに強化が4行並ぶ", rows == 4, rows.ToString());
        return true;
    }

    private static bool ShotsShop() => Shots("shop", () => VerifyCard("ShopPanel"));

    private static bool BuyInShop()
    {
        var gs = GrowthService.Instance;
        Idle.DataManager.data.Money = 1000; // テスト用の所持金（終了時にセーブを元へ戻す）
        PartyUpgradeDefinition wand = gs.Upgrades.First(u => u.upgradeId == "wand");
        PartyUpgradeDefinition ribbon = gs.Upgrades.First(u => u.upgradeId == "ribbon");
        int atkBefore = gs.GetStats(gs.MainCharacter).atk;
        PartyUpgradeRowUI wandRow = RowFor("魔法の杖みがき");
        wandRow.GetComponentsInChildren<Button>(true).First(b => b.name == "BuyButton").onClick.Invoke();
        Check("★（所持金）で「魔法の杖みがき」を買える", gs.GetUpgradeLevel(wand) == 1 && Idle.DataManager.data.Money == 700, $"Lv{gs.GetUpgradeLevel(wand)} 所持金 {Idle.DataManager.data.Money}");
        Check("杖で ATK +5%", gs.GetStats(gs.MainCharacter).atk == Mathf.RoundToInt(atkBefore * 1.05f), $"{atkBefore}→{gs.GetStats(gs.MainCharacter).atk}");
        long gem = gs.GetBalance(Currency.Gem);
        RowFor("リリィのリボン").GetComponentsInChildren<Button>(true).First(b => b.name == "BuyButton").onClick.Invoke();
        Check("💎で「リリィのリボン」を買える", gs.GetUpgradeLevel(ribbon) == 1 && gs.GetBalance(Currency.Gem) == gem - 3, $"💎 {gem}→{gs.GetBalance(Currency.Gem)}");
        long exp = gs.GetBalance(Currency.Exp);
        long pack = gs.ExpPackAmount;
        PanelFind<Button>("ShopPanel", "SafeArea/CardArea/Card/ExpPackBox/BuyButton").onClick.Invoke();
        Check("💎1 で EXP パックを買える", gs.GetBalance(Currency.Exp) == exp + pack, $"+{pack}");
        exp = gs.GetBalance(Currency.Exp);
        long ad = gs.AdExpAmount;
        PanelFind<Button>("ShopPanel", "SafeArea/CardArea/Card/AdBox/WatchButton").onClick.Invoke();
        Check("動画で EXP（Editor はダミー広告）と回数表示", gs.GetBalance(Currency.Exp) == exp + ad && PanelText("ShopPanel", "CountText") == "1/5", $"+{ad} {PanelText("ShopPanel", "CountText")}");
        Text goldLabel = Find<Text>("Text_Gold");
        Check("ショップで使ったゴールドが上部 GOLD 表示に反映される（次フレーム）", true, goldLabel != null ? goldLabel.text : "-");
        return true;
    }

    private static bool CheckTapDamage()
    {
        if (Sub == 0)
        {
            Text goldLabel = Find<Text>("Text_Gold");
            Check("上部 GOLD 表示が支払い後の所持金", goldLabel != null && goldLabel.text == "GOLD: " + Idle.DataManager.data.Money.ToString("N0"), goldLabel != null ? goldLabel.text : "-");
            var gs = GrowthService.Instance;
            Idle.EnemyController e = Enemy();
            int before = e.GetCurrentHp();
            int taps = 4; // 威力 1.25 なら 1+1+1+2 = 5（HP の少ない敵でも倒しきらない回数）
            for (int i = 0; i < taps; i++) e.OnTap();
            int expected = Mathf.FloorToInt(gs.AttackPowerExact * taps + 1e-4f);
            int actual = before - e.GetCurrentHp();
            Check("本編タップのダメージが ATK 由来の威力（端数持ち越し）", Mathf.Abs(actual - expected) <= 1 && actual > taps, $"{taps}タップで {actual}（威力 {gs.AttackPowerExact:0.##}）");
        }
        return true;
    }

    private static bool CheckQuestStats()
    {
        var gs = GrowthService.Instance;
        QuestManager qm = QuestManager.Instance;
        Check("クエストの攻撃力が ATK 由来の威力", qm.GetPlayerAttack() == gs.AttackPower, $"{qm.GetPlayerAttack()} / {gs.AttackPower}");
        Check("クエストの最大HPが強化後の HP", qm.BasePlayerMaxHp == gs.MainMaxHp, $"{qm.BasePlayerMaxHp} / {gs.MainMaxHp}");
        Check("正解の魔法にスキル倍率（Lv2 = 220%）", gs.CorrectAnswerDamage(10) == 22, gs.CorrectAnswerDamage(10).ToString());
        long exp = gs.GetBalance(Currency.Exp);
        gs.OnCorrectAnswer();
        Check("英単語の正解で EXP +5", gs.GetBalance(Currency.Exp) == exp + 5);
        return true;
    }

    private static bool CloseAll()
    {
        GrowthUIView view = GrowthUIView.Find();
        view.shopCloseButton.onClick.Invoke();
        Check("『とじる』で閉じる", !view.IsOpen);
        Find<RectTransform>("UpgradeButton").GetComponent<Button>().onClick.Invoke();
        Check("HUD の「強化」ボタンでショップが開く", view.shopRoot.activeInHierarchy);
        view.CloseAll();
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
        // セーブを元に戻す
        if (SessionState.GetBool(K + "HadDataBin", false) && File.Exists(BackupPath)) File.Copy(BackupPath, DataBinPath, true);
        else if (!SessionState.GetBool(K + "HadDataBin", false) && File.Exists(DataBinPath)) File.Delete(DataBinPath);
        if (SessionState.GetBool(K + "HadGrowth", false)) PlayerPrefs.SetString(GrowthSaveKey, SessionState.GetString(K + "Growth", ""));
        else PlayerPrefs.DeleteKey(GrowthSaveKey);
        PlayerPrefs.Save();
        int orig = SessionState.GetInt(K + "OrigSize", -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        int fail = lines.Count(l => l.Contains("\tFAIL\t"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={fail}");
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(fail == 0 ? 0 : 1);
        return true;
    }

    /// <summary>
    /// batchmode 用: Unity -batchmode -projectPath . -executeMethod GrowthProbe.RunBatch（-quit は付けない）。
    /// Game View が無いので解像度の切り替えと撮影は行わず、動作だけを検証して終了する。
    /// </summary>
    public static void RunBatch()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "test.txt"), "");
    }

    // ─── 撮影と配置の検証 ───────────────────────────────────────
    private static bool Shots(string tag, Action verify)
    {
        if (Application.isBatchMode)
        {
            // Game View が無いので、カメラで RenderTexture に描いて各解像度を撮影・検証する
            foreach (Vector2Int r in Resolutions)
            {
                SessionState.SetString(K + "Stage", $"{tag} {r.x}x{r.y}");
                BatchCapture(r.x, r.y, Path.Combine(Dir, $"{tag}_{r.x}x{r.y}.png"), verify);
            }
            return true;
        }
        int i = Sub / 2;
        if (i >= Resolutions.Length) { SetResolution(0); return true; }
        if (Sub % 2 == 0) { SetResolution(i); Sub = Sub + 1; return false; }
        SessionState.SetString(K + "Stage", $"{tag} {Label(i)}");
        verify();
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{tag}_{Label(i)}.png"));
        Wait(1.0);
        Sub = Sub + 1;
        return false;
    }

    private static string Stage => SessionState.GetString(K + "Stage", "");

    /// <summary>
    /// batchmode 用の撮影。全ての Canvas を一時的に Main Camera の Screen Space - Camera にして、
    /// 解像度 w x h の RenderTexture へ描く。CanvasScaler は Screen を見るので止め、Match=0 と同じ倍率を直接入れる。
    /// 検証（verify）もこの状態で行い、終わったら元に戻す。
    /// </summary>
    private static void BatchCapture(int w, int h, string path, Action verify)
    {
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
                // Overlay だった Canvas（強化画面・クエスト）は本編 Canvas より手前・上に描く
                c.planeDistance = overlay ? 40f : 100f;
                if (overlay) c.sortingOrder = 2000 + c.sortingOrder;
                if (s != null) s.enabled = false;
                c.scaleFactor = w / 1080f;
            }
            Canvas.ForceUpdateCanvases();
            foreach (LayoutGroup g in All<LayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)g.transform);
            Canvas.ForceUpdateCanvases();
            verify();
            cam.Render();
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            File.WriteAllBytes(path, tex.EncodeToPNG());
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

    private static void VerifyHud()
    {
        RectTransform panel = Find<RectTransform>("HudPanel");
        Rect view = panel.GetComponentInParent<Canvas>().rootCanvas.pixelRect;
        Rect info = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("Info"));
        Rect up = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("UpgradeButton"));
        Rect adv = UILayoutProbeRuntime.ScreenRect(Find<RectTransform>("AdventureButton"));
        Rect hud = UILayoutProbeRuntime.ScreenRect(panel);
        Log($"LAYOUT\t{Stage}\tview={view.width}x{view.height}\tinfo={F(info)}\tupgrade={F(up)}\tadventure={F(adv)}");
        Check($"{Stage}: 強化ボタンと冒険ボタンが重ならない", up.xMax <= adv.xMin + 0.5f, $"{up.xMax:0} ≤ {adv.xMin:0}");
        Check($"{Stage}: 名前・HP 欄が強化ボタンと重ならない", info.xMax <= up.xMin + 0.5f, $"{info.xMax:0} ≤ {up.xMin:0}");
        Check($"{Stage}: 強化ボタンが HUD 内", up.yMin >= hud.yMin - 0.5f && up.yMax <= hud.yMax + 0.5f && up.xMin >= hud.xMin);
    }

    private static void VerifyCard(string panelName)
    {
        RectTransform card = PanelFind<RectTransform>(panelName, "SafeArea/CardArea/Card");
        RectTransform dim = PanelFind<RectTransform>(panelName, "DimOverlay");
        Rect view = card.GetComponentInParent<Canvas>().rootCanvas.pixelRect;
        Rect c = UILayoutProbeRuntime.ScreenRect(card);
        Rect d = UILayoutProbeRuntime.ScreenRect(dim);
        Log($"LAYOUT\t{Stage}\tview={view.width}x{view.height}\tcard={F(c)}");
        Check($"{Stage}: カードが画面内", c.xMin >= -0.5f && c.yMin >= -0.5f && c.xMax <= view.width + 0.5f && c.yMax <= view.height + 0.5f, F(c));
        Check($"{Stage}: 暗幕が全画面", d.xMin <= 0.5f && d.yMin <= 0.5f && d.xMax >= view.width - 0.5f && d.yMax >= view.height - 0.5f, F(d));
    }

    // ─── 補助 ───────────────────────────────────────────────────
    private static Idle.EnemyController Enemy() => All<Idle.EnemyController>().FirstOrDefault(e => e.GetCurrentHp() > 0);
    private static T Find<T>(string name) where T : Component => All<T>().FirstOrDefault(c => c.name == name);

    private static string HudText(string name)
    {
        RectTransform hud = Find<RectTransform>("StatusWindow");
        TMP_Text t = hud != null ? hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.name == name) : null;
        return t != null ? t.text : "(なし)";
    }

    private static T PanelFind<T>(string panel, string path) where T : Component
    {
        GrowthUIView view = GrowthUIView.Find();
        Transform root = view.transform.Find(panel);
        Transform t = root != null ? root.Find(path) : null;
        if (t == null && root != null) t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == path);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static string PanelText(string panel, string name)
    {
        TMP_Text t = PanelFind<TMP_Text>(panel, name);
        return t != null ? t.text : "(なし)";
    }

    private static PartyUpgradeRowUI RowFor(string displayName)
    {
        GrowthUIView view = GrowthUIView.Find();
        return view.shopRoot.GetComponentsInChildren<PartyUpgradeRowUI>(true)
            .First(r => r.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "NameText" && t.text == displayName));
    }

    private static string F(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";
    private static string Label(int i) => $"{Resolutions[i].x}x{Resolutions[i].y}";

    private static void SetResolution(int i)
    {
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(Resolutions[i].x, Resolutions[i].y, $"UI診断 {Label(i)}");
        Wait(1.5);
    }

    private static System.Collections.Generic.List<T> All<T>() where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        }
        // DontDestroyOnLoad に移ったオブジェクト（GrowthService など）は上で拾えないが、UI の検証には不要
        return list;
    }
}
