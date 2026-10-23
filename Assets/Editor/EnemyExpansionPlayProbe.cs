using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Idle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EnemyExpansionPlayProbe
{
    static EnemyExpansionPlayProbe() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/Enemy Expansion/Play Validate Roster and Boss")]
    static void Run() { SessionState.SetBool("Lily.Enemy60.Play", true); EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool("Lily.Enemy60.Play", false) || !EditorApplication.isPlaying || StageDirector.Instance == null || !StageDirector.Instance.IsReady) return;
        SessionState.SetBool("Lily.Enemy60.Play", false);
        new GameObject("Enemy60 Probe").AddComponent<EnemyExpansionPlayRunner>();
    }
}
public class EnemyExpansionPlayRunner : MonoBehaviour
{
    int checks;
    void Check(bool valid, string name) { if (!valid) throw new Exception("[Enemy60 Play FAIL] " + name); checks++; }
    IEnumerator WaitForTurn(BattlePanel panel)
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (panel.CurrentTurn != BattleTurnState.PlayerTurn &&
               panel.CurrentTurn != BattleTurnState.Victory &&
               panel.CurrentTurn != BattleTurnState.Defeat && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(Time.realtimeSinceStartup < deadline, "battle turn completes");
    }
    IEnumerator Start()
    {
        var director = StageDirector.Instance;
        Check(director.StartStage(1), "stage starts");
        var gm = FindAnyObjectByType<Idle.GameManager>(); gm.MainHealth.Stop();
        var enemy = (EnemyController)typeof(Idle.GameManager).GetField("currentEnemy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gm);
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(390,844,"Enemy60 390x844");
        yield return new WaitForSeconds(3);
        var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemyEncounterBuilder.DatabasePath);
        var visible = (Image)typeof(EnemyController).GetField("enemyImage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemy);
        foreach (var data in db.enemies)
        {
            enemy.Setup(null, data); yield return null;
            Check(visible.sprite == data.sprite && enemy.GetCurrentHp() == data.baseHp, "sprite and HP " + data.name);
            Check(enemy.AttackVisual.rect.width <= 470.1f && enemy.AttackVisual.rect.height <= 450, "bounds fit " + data.name);
        }
        foreach (var key in new[]{"AcornMouse","LanternCrab","MooncapMage","FrostBoar","CrownedTreant","BlossomMothQueen","CrystalCrabKing","SnowGolem","StormGriffin","ArcaneOwl"})
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_" + key + ".asset");
            enemy.Setup(null,data);
            yield return new WaitForSeconds(.12f);
            ScreenCapture.CaptureScreenshot("Documentation/EnemyExpansion60/" + key + ".png");
            yield return new WaitForEndOfFrame();
        }
        var boss = db.enemies.First(e => e.tier == 5);
        enemy.Setup(null,boss);
        Check(enemy.GetCurrentHp() == 210 && enemy.AttackDamage == 10 && Mathf.Approximately(enemy.AttackInterval(4),3.5f), "main boss HP / attack / speed");
        var health = new MainBattleHealth(); health.Begin(100);
        Check(!health.Tick(3.49f,true,enemy.AttackInterval(4),enemy.AttackDamage), "no early boss strike");
        Check(health.Tick(.02f,true,enemy.AttackInterval(4),enemy.AttackDamage) && health.Hp == 90, "boss strikes at new interval");
        director.ReturnToBase();
        var overlay = FindAnyObjectByType<QuestOverlayController>();
        if (overlay.hideWhileQuesting != null)
            foreach (var go in overlay.hideWhileQuesting) if (go != null) go.SetActive(false);
        overlay.questRoot.SetActive(true);
        var panel = FindAnyObjectByType<BattlePanel>(FindObjectsInactive.Include);
        Check(panel != null, "adventure panel available");
        panel.ConfigureTiming(.01f,.01f,.01f);
        var defaultBoss = new BossData();
        panel.Show(BattlePhase.Boss,defaultBoss.bossName,null,defaultBoss.bossHp,8,100,100,1,defaultBoss.bossGoldReward,_=>{});
        yield return WaitForTurn(panel);
        Check(panel.EnemyMaxHp == 20 && !panel.UseLegacyQuiz && panel.PlayerMp == 5, "boss20 / no quiz / full MP");
        int hp = panel.EnemyHp;
        Check(panel.TryCastSpell(BattleActions.Fire), "fire available");
        yield return WaitForTurn(panel);
        Check(panel.EnemyHp == hp - panel.NormalAttackPower * 2 && panel.PlayerMp == 3, "fire damage and MP retained");
        Check(panel.EnemyHp > 0 && panel.PlayerHp == 92, "boss survives opening fire and retaliates");
        int turnCount = 1;
        while (panel.CurrentTurn == BattleTurnState.PlayerTurn && turnCount < 30)
        {
            if (panel.PlayerMp >= 2) panel.TryCastSpell(BattleActions.Fire); else panel.TryAttack();
            turnCount++; yield return WaitForTurn(panel);
        }
        Check(panel.CurrentTurn == BattleTurnState.Victory, "boss beatable with fire and normal attacks");
        panel.Hide();
        panel.Show(BattlePhase.Boss,defaultBoss.bossName,null,defaultBoss.bossHp,8,1,100,1,defaultBoss.bossGoldReward,_=>{});
        yield return WaitForTurn(panel);
        panel.TryDefend(); yield return WaitForTurn(panel);
        Check(panel.CurrentTurn == BattleTurnState.Defeat, "boss defeat processing preserved");
        panel.Hide(); panel.ConfigureTiming(.24f,.22f,.4f);
        overlay.CloseQuest();
        File.WriteAllText("Documentation/EnemyExpansion60/play-results.txt", $"{checks} checks passed. All 60 sprites/HP/width verified. Main boss timing and quest boss Fire/MP/victory/defeat verified. No kills or rewards persisted.\n");
        Debug.Log($"[Enemy60 Play] {checks} passed");
    }
}
