using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Idle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TravelMotionPreview
{
    const string Key = "Lily.TravelMotionPreview";
    static TravelMotionPreview() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/Main Battle/Validate Travel Motion")]
    static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) throw new Exception("Save the scene before running the presentation check.");
            // Reload after compilation so newly serialized fields are read from disk.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene.path);
        }
        SessionState.SetBool(Key, true);
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || StageDirector.Instance == null || !StageDirector.Instance.IsReady) return;
        SessionState.SetBool(Key, false);
        new GameObject("Travel Motion Preview").AddComponent<TravelMotionPreviewRunner>();
    }
}

public class TravelMotionPreviewRunner : MonoBehaviour
{
    string saved;
    bool hadSave, captured;
    int checks;
    long wallet, passiveIncome;
    bool walletCaptured;
    void Check(bool ok, string label) { if (!ok) throw new Exception("[Travel FAIL] " + label); checks++; }
    static readonly FieldInfo EnemyField = typeof(Idle.GameManager).GetField("currentEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
    IEnumerator Start()
    {
        hadSave = PlayerPrefs.HasKey(StageProgress.SaveKey); saved = PlayerPrefs.GetString(StageProgress.SaveKey); captured = true;
        wallet = DataManager.data.Money; passiveIncome = DataManager.data.MoneyPerSecond;
        DataManager.data.MoneyPerSecond = 0; walletCaptured = true;
        var director = StageDirector.Instance;
        Check(director.StartStage(1), "stage starts");
        var gm = UnityEngine.Object.FindAnyObjectByType<Idle.GameManager>();
        gm.MainHealth.Stop();
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(390, 844, "Travel 390x844");
        yield return new WaitForSecondsRealtime(3);
        var player = gm.playerController;
        var rect = (RectTransform)player.transform;
        var origin = rect.anchoredPosition;
        var scale = rect.localScale;
        var rotation = rect.localRotation;
        var idle = player.GetComponent<Image>().sprite;
        Check(new SerializedObject(player).FindProperty("smoothTravel").boolValue, "continuous travel enabled");
        var running = (Sprite)new SerializedObject(player).FindProperty("runningSprite").objectReferenceValue;
        Check(running != null && running != idle, "dedicated running pose assigned");
        var seen = new HashSet<Sprite>();
        bool moved = false, coinSeen = false;
        Directory.CreateDirectory("Documentation/TravelMotion");
        var enemy = (EnemyController)EnemyField.GetValue(gm);
        // Exercise the real transition without awarding test kills or modifying growth saves.
        CoinSpawner.Instance.SpawnCoinExplosion(enemy.AttackVisual.TransformPoint(enemy.AttackVisual.rect.center), 101);
        gm.OnEnemyDefeated(enemy);
        Destroy(enemy.gameObject);
        float began = Time.time;
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(.08f);
            seen.Add(player.GetComponent<Image>().sprite);
            moved |= rect.anchoredPosition != origin;
            foreach (var coin in UnityEngine.Object.FindObjectsByType<Coin>(FindObjectsSortMode.None))
            {
                coinSeen = true;
                Check(coin.GetComponent<Image>().sprite.name == "Coin_0" && ((RectTransform)coin.transform).sizeDelta.x == 52, "correct coin art and size");
            }
            Check(rect.localScale == scale, "no body proportion changes");
            ScreenCapture.CaptureScreenshot("Documentation/TravelMotion/frame-" + i.ToString("D2") + ".png");
        }
        Check(seen.Count == 1 && seen.Contains(running) && moved, "running pose moves without frame flicker");
        Check(coinSeen, "reward coins visible");
        var dust = UnityEngine.Object.FindAnyObjectByType<TravelFootfallGraphic>();
        Check(dust != null && !dust.raycastTarget, "footfalls visible and do not block taps");
        Check(EnemyField.GetValue(gm) == null, "enemy not spawned early");
        yield return new WaitForSeconds(.3f);
        Check(EnemyField.GetValue(gm) != null && Time.time - began < 1.5f, "next encounter keeps one-second timing");
        Check(gm.currentGold == 101 && DataManager.data.Money == wallet + 101, "exact gold reward collected once");
        Check(UnityEngine.Object.FindObjectsByType<Coin>(FindObjectsSortMode.None).Length == 0, "coins finish and disappear");
        Check(player.GetComponent<Image>().sprite == idle, "idle sprite restored");
        Check(rect.anchoredPosition == origin && rect.localScale == scale && rect.localRotation == rotation, "resting transform restored");
        Check(!dust.gameObject.activeSelf, "footfalls removed on encounter");
        player.SetState(PlayerController.PlayerState.Running);
        yield return new WaitForSeconds(.15f);
        player.Attack();
        Check(!dust.gameObject.activeSelf && rect.localScale == scale && rect.localRotation == rotation, "attack interrupts travel cleanly");
        player.SetState(PlayerController.PlayerState.Idle);
        yield return new WaitForSeconds(.5f);
        Check(rect.anchoredPosition == origin && player.GetComponent<Image>().sprite == idle, "attack returns to idle");
        player.SetState(PlayerController.PlayerState.Running);
        yield return new WaitForSeconds(.15f);
        director.ReturnToBase();
        Check(!dust.gameObject.activeSelf && rect.anchoredPosition == origin && rect.localScale == scale, "retreat clears travel");
        var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Coin.prefab");
        var timeoutCoin = Instantiate(coinPrefab, rect.parent).GetComponent<Coin>();
        typeof(Coin).GetField("hardTimeout", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(timeoutCoin, .05f);
        timeoutCoin.SetGoldValue(7);
        timeoutCoin.Burst(Vector2.zero, rect);
        timeoutCoin.Burst(Vector2.zero, rect); // Repeated launch must not pay twice.
        yield return new WaitForSeconds(.9f);
        Check(DataManager.data.Money == wallet + 108 && timeoutCoin == null, "timeout settles reward once");
        var missingTargetCoin = Instantiate(coinPrefab, rect.parent).GetComponent<Coin>();
        missingTargetCoin.SetGoldValue(3);
        missingTargetCoin.Burst(Vector2.zero, null);
        yield return new WaitForSeconds(.35f);
        Check(DataManager.data.Money == wallet + 111 && missingTargetCoin == null, "missing target still awards reward");
        Restore();
        Debug.Log("[TravelMotion] " + checks + " passed; original encounter timing and resting placement preserved.");
    }
    void Restore()
    {
        if (walletCaptured && DataManager.data != null) { DataManager.data.Money = wallet; DataManager.data.MoneyPerSecond = passiveIncome; walletCaptured = false; }
        if (!captured) return;
        if (hadSave) PlayerPrefs.SetString(StageProgress.SaveKey, saved); else PlayerPrefs.DeleteKey(StageProgress.SaveKey);
        PlayerPrefs.Save(); StageProgress.Reload(); captured = false;
    }
    void OnDestroy() => Restore();
}
