using System.Collections;
using System.IO;
using System.Reflection;
using Idle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Captures the old and new art with the same live characters and restores stage selection.
[InitializeOnLoad]
public static class StageBackgroundPreview
{
    const string Pending = "Lily.StageBackgroundPreview.Pending";
    static StageBackgroundPreview() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/Stage Background/Preview Forest")]
    static void Run()
    {
        SessionState.SetBool(Pending, true);
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
            StageDirector.Instance == null || !StageDirector.Instance.IsReady) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Stage Background Preview").AddComponent<StageBackgroundPreviewRunner>();
    }
}

public class StageBackgroundPreviewRunner : MonoBehaviour
{
    string saved;
    bool existed, captured;
    IEnumerator Start()
    {
        existed = PlayerPrefs.HasKey(StageProgress.SaveKey);
        saved = PlayerPrefs.GetString(StageProgress.SaveKey);
        captured = true;
        var director = StageDirector.Instance;
        if (!director.StartStage(1)) { Restore(); yield break; }
        var gm = Object.FindAnyObjectByType<Idle.GameManager>();
        gm.MainHealth.Stop(); // Keep the presentation fixture still; no runtime setting is changed.
        var enemy = (EnemyController)typeof(Idle.GameManager).GetField("currentEnemy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gm);
        enemy.Setup(null, AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_CrystalTurtle.asset"));
        var background = (Image)typeof(StageDirector).GetField("background", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(director);
        var replacement = StageProgress.BackgroundFor(1);
        if (replacement == null) throw new System.Exception("Stage 1 background could not load");
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(390, 844, "Forest 390x844");
        yield return new WaitForSecondsRealtime(3);
        Directory.CreateDirectory("Documentation/StageBackground");
        var lily = gm.playerController.transform;
        var enemyVisual = enemy.AttackVisual;
        var lilyPosition = lily.position;
        var enemyPosition = enemyVisual.position;
        var lilyScale = lily.localScale;
        var enemyScale = enemyVisual.localScale;
        background.sprite = Resources.Load<Sprite>("Backgrounds/stage_01");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Documentation/StageBackground/before.png");
        yield return new WaitForSecondsRealtime(.5f);
        background.sprite = replacement;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Documentation/StageBackground/after.png");
        yield return new WaitForSecondsRealtime(.5f);
        if (lily.position != lilyPosition || enemyVisual.position != enemyPosition ||
            lily.localScale != lilyScale || enemyVisual.localScale != enemyScale)
            throw new System.Exception("Character transform changed during background preview");
        Debug.Log("[StageBackground] Sprite loaded; Lily/enemy positions and scales unchanged; portrait preview captured.");
        director.ReturnToBase();
        Restore();
    }
    void Restore()
    {
        if (!captured) return;
        if (existed) PlayerPrefs.SetString(StageProgress.SaveKey, saved);
        else PlayerPrefs.DeleteKey(StageProgress.SaveKey);
        PlayerPrefs.Save();
        StageProgress.Reload();
        captured = false;
    }
    void OnDestroy() => Restore();
}
