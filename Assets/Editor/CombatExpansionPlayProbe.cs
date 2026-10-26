using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Idle;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CombatExpansionPlayProbe
{
    const string Key = "CombatExpansion.Pending";
    static CombatExpansionPlayProbe() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/Combat Expansion/Play Validate Tap Effects")]
    static void Run() { SessionState.SetBool(Key,true); if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || StageDirector.Instance == null || !StageDirector.Instance.IsReady) return;
        SessionState.SetBool(Key,false);
        new GameObject("Combat Expansion Probe").AddComponent<CombatExpansionRunner>();
    }
}

public class CombatExpansionRunner : MonoBehaviour
{
    int checks;
    string savedStage;
    bool hadStage, captured;
    void Check(bool value, string label) { if (!value) throw new Exception("[CombatExpansion FAIL] " + label); checks++; }
    IEnumerator Start()
    {
        hadStage=PlayerPrefs.HasKey(StageProgress.SaveKey); savedStage=PlayerPrefs.GetString(StageProgress.SaveKey); captured=true;
        var director=StageDirector.Instance;
        Check(director.StartStage(1),"start main battle");
        var gm=UnityEngine.Object.FindAnyObjectByType<Idle.GameManager>();
        gm.MainHealth.Stop(); // Presentation fixture: no incoming hits while capturing three variants.
        var enemy=(EnemyController)typeof(Idle.GameManager).GetField("currentEnemy",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(gm);
        var data=AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_CrystalTurtle.asset");
        enemy.Setup(null,data);
        var vfx=gm.playerController.gameObject.AddComponent<MainAttackVfx>();
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(390,844,"Tap Magic 390x844");
        yield return new WaitForSecondsRealtime(2);
        foreach(var sound in new[]{SfxDirector.Attack,SfxDirector.Magic,SfxDirector.Hit,SfxDirector.Guard,"se_tap_fire","se_tap_ice","se_tap_thunder"})
        {
            Check(SfxDirector.Play(sound),"sound plays " + sound);
            yield return new WaitForSecondsRealtime(.3f);
        }
        Directory.CreateDirectory("Documentation/CombatExpansion");
        for(int i=0;i<3;i++)
        {
            int hp=enemy.GetCurrentHp();
            gm.Click();
            Check(enemy.GetCurrentHp()<hp,"tap still applies damage");
            Check(vfx.LastStyle==i && vfx.ActiveCount==1,"tap variation " + i);
            yield return new WaitForSeconds(.1f);
            ScreenCapture.CaptureScreenshot("Documentation/CombatExpansion/tap-"+i+".png");
            yield return new WaitForSeconds(.4f);
            Check(vfx.ActiveCount==0,"effect automatically finishes");
        }
        int before=enemy.GetCurrentHp();
        for(int i=0;i<50;i++)vfx.Play(enemy);
        Check(vfx.ActiveCount==6,"rapid taps bounded to six effects");
        Check(enemy.GetCurrentHp()==before,"visuals do not deal extra damage");
        director.ReturnToBase();
        Check(vfx.ActiveCount==0,"leaving battle clears effects");
        Check(BattleActions.Spells.Count==1 && BattleActions.Fire.MpCost==2,"adventure spell list unchanged");
        Restore();
        Debug.Log("[CombatExpansion Play] " + checks + " passed (main tap effects)");
    }
    void Restore()
    {
        if(!captured)return;
        if(hadStage)PlayerPrefs.SetString(StageProgress.SaveKey,savedStage);else PlayerPrefs.DeleteKey(StageProgress.SaveKey);
        PlayerPrefs.Save();captured=false;
    }
    void OnDestroy()=>Restore();
}
