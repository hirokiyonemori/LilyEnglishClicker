using System;
using System.IO;
using System.Reflection;
using Idle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MainBattleProbe
{
    const string Key = "MainBattleProbe.Pending";
    static int step, checks;
    static double deadline;
    static int maxHp;
    static float oldInterval;
    static string stageSave;
    static bool hadStage;
    static Idle.GameManager gm;
    static StageDirector director;
    static FieldInfo Interval => typeof(Idle.GameManager).GetField("enemyAttackInterval", BindingFlags.NonPublic | BindingFlags.Instance);
    static MainBattleProbe() { EditorApplication.update += Update; }
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        checks++; Debug.Log("[MainBattle PASS] " + label);
    }
    [MenuItem("LilyClicker/Main Battle/Validate and Preview Defeat")]
    public static void Run()
    {
        checks = 0;
        var hp = new MainBattleHealth(); hp.Begin(100);
        Check(hp.Hp == 100 && hp.Active, "start at full HP");
        Check(!hp.Tick(3.9f, true, 4, 2) && hp.Hp == 100, "no early attack");
        Check(hp.Tick(.2f, true, 4, 2) && hp.Hp == 98, "enemy interval and damage");
        hp.Tick(3, true, 4, 2); hp.Tick(100, false, 4, 2);
        Check(hp.Hp == 98 && !hp.Tick(1, true, 4, 2), "modal/background pause and resume grace");
        hp.ResetAttackTimer();
        Check(!hp.Tick(3, true, 4, 2), "new enemy gets full interval");
        hp.Tick(5, true, 4, 500);
        Check(hp.Hp == 0 && hp.IsDefeated && !hp.Active, "lethal damage clamps and stops");
        Check(!hp.Tick(100, true, 4, 10), "no attacks after defeat");
        hp.Begin(150); Check(hp.Hp == 150 && !hp.IsDefeated, "retry uses current max HP");
        hp.Stop(); Check(!hp.Tick(10, true, 4, 2), "base has no incoming attacks");
        SessionState.SetInt("MainBattleProbe.Checks", checks);
        SessionState.SetBool(Key, true);
        step = 0; deadline = EditorApplication.timeSinceStartup + 45;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }
    static void Update()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (step == 0)
            {
                director = StageDirector.Instance;
                if (director == null || !director.IsReady) return;
                checks = SessionState.GetInt("MainBattleProbe.Checks", 0);
                gm = UnityEngine.Object.FindAnyObjectByType<Idle.GameManager>();
                hadStage = PlayerPrefs.HasKey(StageProgress.SaveKey);
                stageSave = PlayerPrefs.GetString(StageProgress.SaveKey, "");
                oldInterval = (float)Interval.GetValue(gm);
                Check(director.StartStage(1), "start stage");
                maxHp = gm.MainHealth.MaxHp;
                Check(gm.MainHealth.Hp == maxHp, "stage initialization restores HP");
                Interval.SetValue(gm, 4f);
                step = 1; deadline = EditorApplication.timeSinceStartup + 20;
            }
            else if (step == 1 && gm.MainHealth.Hp < maxHp)
            {
                Check(gm.MainHealth.Hp > 0, "live Update enemy attack reduces HP");
                var feedback = gm.playerController.GetComponent<MainHitFeedback>();
                Check(feedback != null && feedback.GetComponentInChildren<TMPro.TextMeshProUGUI>() != null, "incoming damage feedback and number created");
                Interval.SetValue(gm, 4f);
                step = 10; deadline = EditorApplication.timeSinceStartup + .12;
            }
            else if (step == 10 && EditorApplication.timeSinceStartup >= deadline)
            {
                Directory.CreateDirectory("Documentation/MainGameOver");
                ScreenCapture.CaptureScreenshot("Documentation/MainGameOver/incoming-hit.png");
                step = 11; deadline = EditorApplication.timeSinceStartup + 1;
            }
            else if (step == 11 && EditorApplication.timeSinceStartup >= deadline)
            {
                Check(gm.playerController.transform.Find("IncomingDamage") == null, "damage number cleaned up after animation");
                Interval.SetValue(gm, .5f);
                gm.MainHealth.Tick(4, true, 4, Math.Max(1, gm.MainHealth.Hp - 1));
                step = 2; deadline = EditorApplication.timeSinceStartup + 20;
            }
            else if (step == 2 && director.CurrentPhase == StageDirector.Phase.Defeated)
            {
                var view = UnityEngine.Object.FindAnyObjectByType<StageDefeatView>(FindObjectsInactive.Include);
                Check(view != null && view.gameObject.activeInHierarchy, "game over view displayed");
                Check(gm.MainHealth.Hp == 0 && !director.IsBattling, "defeat stops battle");
                long gold = DataManager.data.Money;
                gm.Click();
                Check(DataManager.data.Money == gold, "tap cannot grant gold after defeat");
                var retry = (Button)typeof(StageDefeatView).GetField("retryButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                retry.onClick.Invoke();
                Check(director.IsBattling && gm.MainHealth.Hp == gm.MainHealth.MaxHp && !view.gameObject.activeSelf, "retry button restores HP and closes defeat");
                var toBase = (Button)typeof(StageDefeatView).GetField("baseButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                director.OnPlayerDefeated(); toBase.onClick.Invoke();
                Check(director.CurrentPhase == StageDirector.Phase.Base && !gm.MainHealth.Active && !view.gameObject.activeSelf, "base button stops combat");
                director.StartStage(1);
                gm.MainHealth.Tick(4, true, 4, gm.MainHealth.MaxHp);
                director.OnPlayerDefeated();
                Interval.SetValue(gm, oldInterval);
                if (hadStage) PlayerPrefs.SetString(StageProgress.SaveKey, stageSave); else PlayerPrefs.DeleteKey(StageProgress.SaveKey);
                PlayerPrefs.Save();
                UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(390, 844, "Game Over 390x844");
                step = 3; deadline = EditorApplication.timeSinceStartup + 3;
            }
            else if (step == 3 && EditorApplication.timeSinceStartup > deadline)
            {
                Directory.CreateDirectory("Documentation/MainGameOver");
                ScreenCapture.CaptureScreenshot("Documentation/MainGameOver/390x844.png");
                Debug.Log("[MainBattle RESULT] " + checks + " passed");
                SessionState.SetBool(Key, false);
            }
            if (step != 3 && deadline > 0 && EditorApplication.timeSinceStartup > deadline) throw new Exception("Play validation timed out");
        }
        catch (Exception e)
        {
            SessionState.SetBool(Key, false);
            if (gm != null) Interval.SetValue(gm, oldInterval);
            if (stageSave != null) { if (hadStage) PlayerPrefs.SetString(StageProgress.SaveKey, stageSave); else PlayerPrefs.DeleteKey(StageProgress.SaveKey); PlayerPrefs.Save(); }
            Debug.LogException(e);
        }
    }
}
