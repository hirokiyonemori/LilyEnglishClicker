using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using Idle;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoAttackProbe
{
    static AutoAttackProbe() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/Main Battle/Validate Auto Attack and Capacity")]
    static void Run() { SessionState.SetBool("Lily.AutoProbe", true); EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool("Lily.AutoProbe", false) || !EditorApplication.isPlaying || StageDirector.Instance == null || !StageDirector.Instance.IsReady) return;
        SessionState.SetBool("Lily.AutoProbe", false);
        new GameObject("Auto Attack Probe").AddComponent<AutoAttackProbeRunner>();
    }
}
public class AutoAttackProbeRunner : MonoBehaviour
{
    int checks;
    void Check(bool value, string label) { if (!value) throw new Exception("[AutoAttack FAIL] " + label); checks++; }
    IEnumerator Start()
    {
        var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>("Assets/Data/Enemies/EnemyDatabase.asset");
        var textures = db.enemies.Where(e => e != null && e.sprite != null).Select(e => e.sprite.texture).Distinct().ToArray();
        long source = 0, memory = 0;
        var report = new System.Text.StringBuilder("texture,source_bytes,width,height,editor_runtime_bytes,ios_max_size,ios_format,ios_override,readable\n");
        foreach (var tex in textures)
        {
            var path = AssetDatabase.GetAssetPath(tex);
            var bytes = new FileInfo(path).Length;
            var runtime = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var ios = importer.GetPlatformTextureSettings("iPhone");
            source += bytes; memory += runtime;
            report.AppendLine($"{path},{bytes},{tex.width},{tex.height},{runtime},{ios.maxTextureSize},{ios.format},{ios.overridden},{importer.isReadable}");
        }
        Directory.CreateDirectory("Documentation/AutoAttack");
        File.WriteAllText("Documentation/AutoAttack/enemy-capacity.csv", report.ToString());
        Debug.Log($"[EnemyCapacity] enemies={db.enemies.Count} textures={textures.Length} sourceBytes={source} editorTextureBytes={memory}");
        var director = StageDirector.Instance;
        Check(director.StartStage(1), "stage starts");
        var gm = FindAnyObjectByType<Idle.GameManager>();
        var enemyField = typeof(Idle.GameManager).GetField("currentEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        var enemy = (EnemyController)enemyField.GetValue(gm);
        var hpField = typeof(EnemyController).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic);
        hpField.SetValue(enemy, 10000); // No kill/save/reward changes in this fixture.
        int before = enemy.GetCurrentHp();
        yield return new WaitForSeconds(1.65f);
        int automatic = before - enemy.GetCurrentHp();
        float power = LilyEnglish.Growth.GrowthService.Instance.AttackPowerExact;
        Check(automatic == Mathf.Max(1, Mathf.FloorToInt(power * .75f)), "automatic damage is 75 percent with minimum one");
        before = enemy.GetCurrentHp(); gm.Click();
        Check(before - enemy.GetCurrentHp() == Mathf.Max(1, Mathf.FloorToInt(power)), "tap retains full power");
        gm.MainHealth.Stop(); before = enemy.GetCurrentHp();
        yield return new WaitForSeconds(1.7f);
        Check(enemy.GetCurrentHp() == before, "inactive battle stops auto attack");
        gm.MainHealth.Begin(1000);
        yield return new WaitForSeconds(1.65f);
        Check(enemy.GetCurrentHp() < before, "auto resumes after full interval");
        director.ReturnToBase();
        yield return new WaitForSeconds(1.7f);
        Check(enemyField.GetValue(gm) == null, "base has no attacks or new enemies");
        Debug.Log($"[AutoAttack] {checks} passed");
    }
}
