using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【診断用】クエスト各画面で、背景（Resources/Backgrounds）が意図どおり切り替わるかを Play で確認して撮影する。
/// Logs/QuestBackgroundProbe/test.txt を置くと開始。結果は result.tsv、画像は bg_*.png。
/// QuestManager の状態は確認のために直接書き換えるだけで、シーンは保存しない。
/// </summary>
[InitializeOnLoad]
public static class QuestBackgroundProbe
{
    private const string Dir = "Logs/QuestBackgroundProbe";
    private const string K = "QuestBackgroundProbe.";

    private sealed class Case
    {
        public string Label, Expected;
        public QuestState State;
        public bool PathLeft;
        public Action<QuestManager> Setup;
    }

    private static readonly Case[] Cases =
    {
        new Case { Label = "1_choice",       State = QuestState.ShowingChoice, PathLeft = true,  Expected = "Background_0",
                   Setup = m => m.choicePanel.Show(0, 3, _ => { }) },
        new Case { Label = "2_village",      State = QuestState.VillagerEvent, PathLeft = true,  Expected = "village",
                   Setup = m => m.villagerPanel.Show(DefaultVillagerScenarios.Get()[0], _ => { }) },
        new Case { Label = "3_chest",        State = QuestState.ChestEvent,    PathLeft = true,  Expected = "ruins",
                   Setup = m => m.chestPanel.Show(null, _ => { }) },
        new Case { Label = "4_battle_path",  State = QuestState.EnemyBattle,   PathLeft = true,  Expected = "path_forest",
                   Setup = m => ShowBattle(m, BattlePhase.MidPath) },
        new Case { Label = "5_battle_road",  State = QuestState.EnemyBattle,   PathLeft = false, Expected = "path_road",
                   Setup = m => ShowBattle(m, BattlePhase.MidPath) },
        new Case { Label = "6_boss",         State = QuestState.BossBattle,    PathLeft = false, Expected = "boss",
                   Setup = m => ShowBattle(m, BattlePhase.Boss) },
        new Case { Label = "7_clear",        State = QuestState.QuestClear,    PathLeft = false, Expected = "Background_0",
                   Setup = m => m.clearPanel.Show("はじめての冒険", "クエストクリア！\n見事な冒険だった！", 420, 80, () => { }) },
    };

    static QuestBackgroundProbe()
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
            string p = Path.Combine(Dir, "test.txt");
            if (!File.Exists(p)) return;
            File.Delete(p);
            File.Delete(Path.Combine(Dir, "result.tsv"));
            foreach (string f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);
            Log("INFO\t開始");
            Index = 0; Sub = 0; Wait(0);
            SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            return;
        }

        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(K + "StepStart", 0f) > 120)
        {
            Check($"タイムアウト(step {Index})", false);
            Finish();
            return;
        }
        if (Waiting) { UILayoutDiagnostics.GameViewSizeTool.Repaint(); return; }

        try { Step(); }
        catch (Exception e) { Check($"例外(step {Index})", false, e.ToString().Replace('\n', ' ')); Finish(); }
    }

    // Index: 0=準備 1=Play開始 2=準備待ち 3..=各ケース 末尾=Play終了 → 終了処理
    private static void Step()
    {
        int caseCount = Cases.Length;
        if (Index == 0)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path != "Assets/Scenes/Game.unity")
            {
                if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Finish(); return; }
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            }
            SessionState.SetInt(K + "OrigSize", UILayoutDiagnostics.GameViewSizeTool.CurrentIndex());
            UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(1080, 1920, "背景確認 1080x1920");
            Wait(1.5); Next(); return;
        }
        if (Index == 1) { if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true; Next(); return; }
        if (Index == 2)
        {
            if (!EditorApplication.isPlaying || QuestManager.Instance == null || Backdrop() == null) return;
            if (Sub == 0) { Sub = 1; Wait(2.5); return; }
            OpenQuestView(); Next(); return;
        }
        int ci = Index - 3;
        if (ci < caseCount)
        {
            Case c = Cases[ci];
            QuestManager m = QuestManager.Instance;
            if (Sub == 0)
            {
                HideAllPanels(m);
                SetProp(m, "CurrentState", c.State);
                SetProp(m, "LastPathWasLeft", c.PathLeft);
                c.Setup(m);
                Sub = 1; Wait(1.8); return;
            }
            string name = Backdrop().sprite != null ? Backdrop().sprite.name : "(なし)";
            Check($"{c.Label}: 背景 = {c.Expected}", name == c.Expected, name);
            if (ci == 0)
            {
                Check("道の選択: 左カードの絵 = path_forest", SpriteName(m.choicePanel, "leftIcon") == "path_forest", SpriteName(m.choicePanel, "leftIcon"));
                Check("道の選択: 右カードの絵 = path_road", SpriteName(m.choicePanel, "rightIcon") == "path_road", SpriteName(m.choicePanel, "rightIcon"));
            }
            // 明るい森向けの紺文字: 暗い背景ではクリーム色、それ以外は元の紺色
            var inkLike = UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>().questRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)
                .Where(t => t.gameObject.name == "EnemyNameText" || t.gameObject.name == "HeaderText" || t.gameObject.name == "ChestTitle").ToArray();
            if (inkLike.Length == 0) Check($"{c.Label}: 紺文字ラベルが見つからない", false);
            else
            {
                bool wantCream = QuestBackgrounds.IsDark(c.Expected);
                Color col = inkLike[0].color;
                bool isCream = col.r > 0.9f && col.g > 0.85f;
                Check($"{c.Label}: 見出し文字 = {(wantCream ? "クリーム" : "紺")}", isCream == wantCream, $"{inkLike[0].name} {col}");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"bg_{c.Label}.png"));
            Wait(1.0); Next(); return;
        }
        if (Index == 3 + caseCount) { if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; Next(); return; }
        Finish();
    }

    private static void Next()
    {
        Sub = 0;
        Index = Index + 1;
        SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
    }

    private static void Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; Wait(1.0); return; }
        int orig = SessionState.GetInt(K + "OrigSize", -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={lines.Count(l => l.Contains("\tFAIL\t"))}");
        Index = -1;
    }

    private static void OpenQuestView()
    {
        QuestOverlayController overlay = UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>();
        if (overlay.hideWhileQuesting != null)
            foreach (GameObject go in overlay.hideWhileQuesting) if (go != null) go.SetActive(false);
        if (overlay.questRoot != null) overlay.questRoot.SetActive(true);
    }

    private static void HideAllPanels(QuestManager m)
    {
        m.choicePanel.Hide(); m.villagerPanel.Hide(); m.chestPanel.Hide(); m.battlePanel.Hide(); m.clearPanel.Hide();
    }

    private static void ShowBattle(QuestManager m, BattlePhase phase)
    {
        var word = new QuestWordData("sword", "ソード", "剣");
        m.battlePanel.Show(phase, phase == BattlePhase.Boss ? "ダークドラゴン" : "まものの sword", word, 10, 1, 100, 100, 1, 100, _ => { });
    }

    private static void SetProp(object target, string name, object value)
    {
        PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p.GetSetMethod(true).Invoke(target, new[] { value });
    }

    private static string SpriteName(Component panel, string field)
    {
        Image image = new SerializedObject(panel).FindProperty(field).objectReferenceValue as Image;
        return image != null && image.sprite != null ? image.sprite.name : "(なし)";
    }

    private static Image Backdrop()
    {
        QuestOverlayController overlay = UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>();
        if (overlay == null || overlay.questRoot == null) return null;
        Transform t = overlay.questRoot.transform.Find("ForestBackground");
        return t != null ? t.GetComponent<Image>() : null;
    }
}
