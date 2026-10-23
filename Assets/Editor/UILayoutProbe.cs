using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 【診断用】Logs/UILayoutProbe/request.txt が置かれたら、Edit 時の値記録 → Play 開始（各タイミング記録）
/// → Game View を 4 解像度へ切替えて本編・Quest 画面を記録/撮影 → Play 終了、までを自動で行う。
/// Play 中の値は UILayoutProbeRuntime が記録する。シーンや Prefab は変更・保存しない。
/// </summary>
[InitializeOnLoad]
public static class UILayoutProbe
{
    private const string StepKey = "UILayoutProbe.Step";
    private const string IndexKey = "UILayoutProbe.Index";
    private const string WaitUntilKey = "UILayoutProbe.WaitUntil";
    private const string OrigSizeKey = "UILayoutProbe.OrigSize";
    private const string StepStartKey = "UILayoutProbe.StepStart";
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
    };

    private enum Step { Idle, EditSweep, EnterPlay, WaitTimeline, PlaySweep, OpenQuest, QuestSweep, ExitPlay }
    private static double nextPoll;

    static UILayoutProbe()
    {
        Directory.CreateDirectory(UILayoutProbeRuntime.Dir);
        File.WriteAllText(Path.Combine(UILayoutProbeRuntime.Dir, "ready.txt"), $"{DateTime.Now:o}\tstep={CurrentStep}\tplaying={EditorApplication.isPlayingOrWillChangePlaymode}");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static Step CurrentStep { get => (Step)SessionState.GetInt(StepKey, 0); set { SessionState.SetInt(StepKey, (int)value); SessionState.SetFloat(StepStartKey, (float)EditorApplication.timeSinceStartup); } }
    private static int Index { get => SessionState.GetInt(IndexKey, 0); set => SessionState.SetInt(IndexKey, value); }
    private static bool Captured { get => SessionState.GetBool("UILayoutProbe.Captured", false); set => SessionState.SetBool("UILayoutProbe.Captured", value); }
    private static void WaitSeconds(double s) => SessionState.SetFloat(WaitUntilKey, (float)(EditorApplication.timeSinceStartup + s));
    private static bool Waiting => EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitUntilKey, 0f);
    private static void Status(string msg) => File.AppendAllText(Path.Combine(UILayoutProbeRuntime.Dir, "status.txt"), $"{DateTime.Now:HH:mm:ss}\t{msg}\n");

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (CurrentStep == Step.Idle)
        {
            if (now < nextPoll) return;
            nextPoll = now + 0.5;
            string cmp = Path.Combine(UILayoutProbeRuntime.Dir, "compare.txt");
            if (File.Exists(cmp) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(cmp);
                CompareBuilderWithPrefab();
                return;
            }
            string req = Path.Combine(UILayoutProbeRuntime.Dir, "request.txt");
            if (!File.Exists(req) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(req);
            File.Delete(Path.Combine(UILayoutProbeRuntime.Dir, "timeline.tsv"));
            Status("開始");
            SessionState.SetInt(OrigSizeKey, UILayoutDiagnostics.GameViewSizeTool.CurrentIndex());
            Index = 0;
            CurrentStep = Step.EditSweep;
            SetResolution(0);
            return;
        }

        if (now - SessionState.GetFloat(StepStartKey, (float)now) > 180)
        {
            Status($"タイムアウト: {CurrentStep}");
            Finish();
            return;
        }
        if (Waiting) { UILayoutDiagnostics.GameViewSizeTool.Repaint(); return; }

        try
        {
            switch (CurrentStep)
            {
                case Step.EditSweep:
                    UILayoutProbeRuntime.Snapshot($"E_Edit {Label(Index)}", Combined());
                    if (++Index < Resolutions.Length) { SetResolution(Index); return; }
                    SetResolution(0);
                    CurrentStep = Step.EnterPlay;
                    return;
                case Step.EnterPlay:
                    SessionState.SetBool(UILayoutProbeRuntime.ActiveKey, true);
                    CurrentStep = Step.WaitTimeline;
                    Status("Play開始");
                    EditorApplication.isPlaying = true;
                    return;
                case Step.WaitTimeline:
                    if (!EditorApplication.isPlaying || !UILayoutProbeRuntime.TimelineDone) return;
                    Index = 0;
                    CurrentStep = Step.PlaySweep;
                    SetResolution(0);
                    return;
                case Step.PlaySweep:
                    // 1回目: 記録と撮影要求 → 撮影が書き出されるのを待ってから次の解像度へ
                    if (!Captured) { Capture($"S_Play本編 {Label(Index)}", UILayoutProbeRuntime.MainTargets, "main"); return; }
                    Captured = false;
                    if (++Index < Resolutions.Length) { SetResolution(Index); return; }
                    CurrentStep = Step.OpenQuest;
                    return;
                case Step.OpenQuest:
                    QuestOverlayController overlay = UnityEngine.Object.FindFirstObjectByType<QuestOverlayController>();
                    if (overlay != null) overlay.OpenQuest();
                    Status(overlay != null ? "Quest開始" : "QuestOverlayController が見つかりません");
                    Index = 0;
                    CurrentStep = Step.QuestSweep;
                    SetResolution(0);
                    WaitSeconds(2.0);
                    return;
                case Step.QuestSweep:
                    if (!Captured) { Capture($"Q_Play Quest画面 {Label(Index)}", UILayoutProbeRuntime.QuestTargets, "quest"); return; }
                    Captured = false;
                    if (++Index < Resolutions.Length) { SetResolution(Index); return; }
                    if (QuestManager.Instance != null) QuestManager.Instance.AbortQuest();
                    CurrentStep = Step.ExitPlay;
                    return;
                case Step.ExitPlay:
                    Finish();
                    return;
            }
        }
        catch (Exception e)
        {
            Status($"例外 {CurrentStep}: {e}");
            Finish();
        }
    }

    private static (string, string)[] Combined()
    {
        var list = new System.Collections.Generic.List<(string, string)>(UILayoutProbeRuntime.MainTargets);
        list.AddRange(UILayoutProbeRuntime.QuestTargets);
        return list.ToArray();
    }

    private static string Label(int i) => $"{Resolutions[i].x}x{Resolutions[i].y}";

    private static void SetResolution(int i)
    {
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(Resolutions[i].x, Resolutions[i].y, $"UI診断 {Label(i)}");
        WaitSeconds(1.5);
    }

    private static void Capture(string stage, (string, string)[] targets, string kind)
    {
        UILayoutProbeRuntime.Snapshot(stage, targets);
        ScreenCapture.CaptureScreenshot(Path.Combine(UILayoutProbeRuntime.Dir, $"{kind}_{Label(Index)}.png"));
        Captured = true;
        WaitSeconds(1.0);
    }

    /// <summary>
    /// Builder の生成結果（BuildView）を一時シーンに作り、保存済み QuestUIRoot.prefab と全 RectTransform を比較する。
    /// 一時シーンは保存せずに閉じる。
    /// </summary>
    private static void CompareBuilderWithPrefab()
    {
        var sb = new StringBuilder();
        Scene active = SceneManager.GetActiveScene();
        Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(temp);
            QuestUIView built = QuestProductionUIBuilder.BuildView();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestProductionUIBuilder.PrefabPath);
            Dictionary<string, RectTransform> a = IndexRects(built.transform), b = IndexRects(prefab.transform);
            int diff = 0;
            foreach (var pair in a)
            {
                if (!b.TryGetValue(pair.Key, out RectTransform p)) { sb.AppendLine($"ONLY_BUILDER\t{pair.Key}"); diff++; continue; }
                RectTransform g = pair.Value;
                string d = "";
                if (!Near(g.anchorMin, p.anchorMin)) d += $" anchorMin {g.anchorMin}→{p.anchorMin}";
                if (!Near(g.anchorMax, p.anchorMax)) d += $" anchorMax {g.anchorMax}→{p.anchorMax}";
                if (!Near(g.pivot, p.pivot)) d += $" pivot {g.pivot}→{p.pivot}";
                if (!Near(g.anchoredPosition, p.anchoredPosition)) d += $" pos {g.anchoredPosition}→{p.anchoredPosition}";
                if (!Near(g.sizeDelta, p.sizeDelta)) d += $" size {g.sizeDelta}→{p.sizeDelta}";
                if (d.Length > 0) { sb.AppendLine($"DIFF\t{pair.Key}\t{d}"); diff++; }
            }
            foreach (string key in b.Keys) if (!a.ContainsKey(key)) { sb.AppendLine($"ONLY_PREFAB\t{key}"); diff++; }
            sb.Insert(0, $"#SUMMARY\tbuilder={a.Count}\tprefab={b.Count}\tdifferences={diff}\n");
        }
        catch (Exception e)
        {
            sb.AppendLine($"#ERROR\t{e}");
        }
        finally
        {
            if (active.IsValid()) SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(temp, true);
        }
        File.WriteAllText(Path.Combine(UILayoutProbeRuntime.Dir, "builder_compare.tsv"), sb.ToString());
        Status("Builder比較 完了");
    }

    /// <summary>batchmode 用: Unity -batchmode -executeMethod UILayoutProbe.CompareBuilderBatch</summary>
    public static void CompareBuilderBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        CompareBuilderWithPrefab();
        EditorApplication.Exit(0);
    }

    private static bool Near(Vector2 x, Vector2 y) => (x - y).sqrMagnitude < 0.0001f;

    private static Dictionary<string, RectTransform> IndexRects(Transform root)
    {
        var map = new Dictionary<string, RectTransform>();
        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.transform == root) continue;
            string path = AnimationUtility.CalculateTransformPath(rt, root);
            string key = path;
            for (int n = 2; map.ContainsKey(key); n++) key = $"{path}#{n}";
            map[key] = rt;
        }
        return map;
    }

    private static void Finish()
    {
        SessionState.SetBool(UILayoutProbeRuntime.ActiveKey, false);
        Captured = false;
        int orig = SessionState.GetInt(OrigSizeKey, -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        CurrentStep = Step.Idle;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        Status("完了");
    }
}
