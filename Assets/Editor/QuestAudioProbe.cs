using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class QuestAudioProbe
{
    const string Pending = "Lily.QuestAudio.Pending";
    static QuestAudioProbe() { EditorApplication.update += Tick; }
    [MenuItem("LilyClicker/Quest Audio/Play Validate Chest and Music")]
    static void Run() { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
            MusicDirector.Instance == null || QuestManager.Instance == null) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Quest Audio Probe").AddComponent<QuestAudioProbeRunner>();
    }
}

public class QuestAudioProbeRunner : MonoBehaviour
{
    int checks;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("[QuestAudio FAIL] " + name);
        checks++;
    }
    void State(QuestState state) => typeof(QuestManager).GetProperty("CurrentState").GetSetMethod(true)
        .Invoke(QuestManager.Instance, new object[]{state});
    IEnumerator Music(QuestState state, string track, bool loop)
    {
        State(state);
        yield return new WaitForSecondsRealtime(1.25f);
        var d = MusicDirector.Instance;
        Check(d.CurrentTrack == track && d.IsLooping == loop, state + " track/loop");
        Check(d.GetComponents<AudioSource>().Any(s => s.isPlaying && s.clip != null && s.clip.name == track), state + " actual playback");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        // Only presentation/state routing is exercised: never complete a quest or grant rewards.
        yield return Music(QuestState.Idle, MusicDirector.Field, true);
        yield return Music(QuestState.ShowingChoice, MusicDirector.Quest, true);
        yield return Music(QuestState.EnemyBattle, MusicDirector.Boss, true);
        yield return Music(QuestState.ShowingChoice, MusicDirector.Quest, true);
        yield return Music(QuestState.BossBattle, MusicDirector.Boss, true);
        yield return Music(QuestState.QuestClear, MusicDirector.Clear, false);
        yield return Music(QuestState.GameOver, MusicDirector.GameOver, false);
        yield return Music(QuestState.Idle, MusicDirector.Field, true);

        var clip = Resources.Load<AudioClip>("Sound/" + SfxDirector.ChestOpen);
        Check(clip != null && clip.length > 0, "chest clip imported");
        Check(SfxDirector.Play(SfxDirector.ChestOpen), "chest clip can play");
        var sfx = FindAnyObjectByType<SfxDirector>();
        var cursor = typeof(SfxDirector).GetField("nextVoice", Private);
        var overlay = FindAnyObjectByType<QuestOverlayController>();
        overlay.questRoot.SetActive(true);
        var chest = FindAnyObjectByType<ChestEventPanel>(FindObjectsInactive.Include);
        var button = (Button)typeof(ChestEventPanel).GetField("openButton", Private).GetValue(chest);
        var resultBox = (GameObject)typeof(ChestEventPanel).GetField("resultBox", Private).GetValue(chest);
        var weights = new[]{"normalWeight", "missWeight", "rareWeight"};
        var oldWeights = weights.Select(w => (int)typeof(ChestEventPanel).GetField(w, Private).GetValue(chest)).ToArray();
        for (int rarity = 0; rarity < 3; rarity++)
        {
            for (int i = 0; i < 3; i++) typeof(ChestEventPanel).GetField(weights[i], Private).SetValue(chest, i == rarity ? 1 : 0);
            chest.Show(null, _ => {});
            int before = (int)cursor.GetValue(sfx);
            button.onClick.Invoke(); button.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 2;
            while ((int)cursor.GetValue(sfx) == before && Time.realtimeSinceStartup < deadline) yield return null;
            Check((int)cursor.GetValue(sfx) == (before + 1) % 4, "one opening sound, rarity " + rarity);
            Check(sfx.GetComponents<AudioSource>().Any(s => s.isPlaying), "opening voice playing, rarity " + rarity);
            yield return new WaitForSecondsRealtime(.5f);
            Check(resultBox.activeInHierarchy, "result still shown, rarity " + rarity);
            Check((int)cursor.GetValue(sfx) == (before + 1) % 4, "no duplicate opening sound, rarity " + rarity);
            chest.Hide();
        }
        for (int i = 0; i < 3; i++) typeof(ChestEventPanel).GetField(weights[i], Private).SetValue(chest, oldWeights[i]);
        overlay.CloseQuest();
        State(QuestState.Idle);
        Directory.CreateDirectory("Documentation/QuestAudio");
        File.WriteAllText("Documentation/QuestAudio/test-results.txt", checks + " checks passed: actual music playback/transitions, three chest outcomes, duplicate-click guard. No rewards granted.\n");
        Debug.Log("[QuestAudio] " + checks + " passed");
    }
}
