using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主要UI（GOLD / SCORE / Lily / Quest開始カード / 下部HUD）の RectTransform と画面上の位置を記録する診断ツール。
/// Edit と Play で同じメニューを実行し、Logs/UILayout/ に出力されたファイルを比較する。
/// Play 中は Game View を 4 解像度へ順に切り替え、各解像度のログとスクリーンショットも残せる。
/// </summary>
public static class UILayoutDiagnostics
{
    private const string OutputDir = "Logs/UILayout";

    private static readonly (string label, string pathSuffix)[] Targets =
    {
        ("GOLD 背景", "PnlSafe/Text_Gold_BG"), ("GOLD アイコン", "PnlSafe/Icon_Gold"), ("GOLD テキスト", "PnlSafe/Text_Gold"),
        ("SCORE 背景", "PnlSafe/Text_Score_BG"), ("SCORE アイコン", "PnlSafe/Icon_Score"), ("SCORE テキスト", "PnlSafe/Text_Score"),
        ("Lily 立ち絵", "PnlSafe/Player"), ("下部HUD", "PnlSafe/StatusWindow"), ("本編 背景", "PnlSafe/Background"),
        ("本編 SafeArea", "PnlTop/PnlSafe"), ("Quest SafeArea", "QuestUIRoot/LaunchSafeArea"), ("入口ダイアログ", "DialogSafeArea/QuestDialogCard"),
    };

    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
    };

    [MenuItem("LilyClicker/UI診断/主要UIのRectTransformをログ出力")]
    public static void DumpCurrent()
    {
        string path = Write(BuildReport(out _), EditorApplication.isPlaying ? "Play" : "Edit");
        Debug.Log($"[UILayoutDiagnostics] 出力しました: {path}");
    }

    // ─── レポート作成 ────────────────────────────────────────────
    private static string BuildReport(out int outsideCount)
    {
        outsideCount = 0;
        var sb = new StringBuilder();
        Rect safe = Screen.safeArea;
        sb.AppendLine($"mode={(EditorApplication.isPlaying ? "Play" : "Edit")} screen={Screen.width}x{Screen.height} safeArea={safe} validated={SafeAreaUtility.Validate(safe, Screen.width, Screen.height)}");
        foreach (CanvasScaler scaler in FindAll<CanvasScaler>())
        {
            Canvas canvas = scaler.GetComponent<Canvas>();
            sb.AppendLine($"CanvasScaler {PathOf(scaler.transform)} mode={canvas.renderMode} ref={scaler.referenceResolution} match={scaler.matchWidthOrHeight} scaleFactor={canvas.scaleFactor:0.###} canvasSize={((RectTransform)canvas.transform).rect.size}");
        }
        sb.AppendLine();
        sb.AppendLine("label\tactive\tanchorMin\tanchorMax\tpivot\tanchoredPosition\tsizeDelta\toffsetMin\toffsetMax\tscreenRect(px)\tinsideScreen");
        List<RectTransform> all = FindAll<RectTransform>();
        foreach (var (label, suffix) in Targets)
        {
            RectTransform rt = all.FirstOrDefault(r => PathOf(r).EndsWith(suffix, StringComparison.Ordinal));
            if (rt == null) { sb.AppendLine($"{label}\t(見つかりません: {suffix})"); continue; }
            Rect screen = ScreenRect(rt);
            bool inside = screen.xMin >= -0.5f && screen.yMin >= -0.5f && screen.xMax <= Screen.width + 0.5f && screen.yMax <= Screen.height + 0.5f;
            if (!inside && rt.gameObject.activeInHierarchy) outsideCount++;
            sb.AppendLine($"{label}\t{rt.gameObject.activeInHierarchy}\t{rt.anchorMin}\t{rt.anchorMax}\t{rt.pivot}\t{rt.anchoredPosition}\t{rt.sizeDelta}\t{rt.offsetMin}\t{rt.offsetMax}\t{Fmt(screen)}\t{(inside ? "OK" : "画面外")}");
        }
        return sb.ToString();
    }

    private static Rect ScreenRect(RectTransform rt)
    {
        Canvas canvas = rt.GetComponentInParent<Canvas>(true);
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 c in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

    private static string Write(string text, string tag)
    {
        Directory.CreateDirectory(OutputDir);
        string path = $"{OutputDir}/{tag}_{Screen.width}x{Screen.height}_{DateTime.Now:yyyyMMdd_HHmmss}.tsv";
        File.WriteAllText(path, text);
        return path;
    }

    private static List<T> FindAll<T>() where T : Component
    {
        var list = new List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return list;
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    // ─── 4解像度の自動検証（Play中） ────────────────────────────
    private static int step;
    private static int waitFrames;
    private static int originalSizeIndex = -1;
    private static readonly StringBuilder summary = new StringBuilder();

    [MenuItem("LilyClicker/UI診断/4解像度を順に切替えて検証（Play中に実行）")]
    public static void RunResolutionSweep()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("UI診断", "Play中に実行してください（SafeArea と PlayerController は Play 中しか動きません）。", "OK");
            return;
        }
        if (GameViewSizeTool.CurrentIndex() < 0)
        {
            EditorUtility.DisplayDialog("UI診断", "Game View が見つかりません。Device Simulator ではなく Game View タブを表示して再実行してください。", "OK");
            return;
        }
        originalSizeIndex = GameViewSizeTool.CurrentIndex();
        step = 0;
        summary.Clear();
        summary.AppendLine("resolution\t画面外の要素数\tログ");
        ApplyStep();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void ApplyStep()
    {
        Vector2Int res = Resolutions[step];
        GameViewSizeTool.SetFixedResolution(res.x, res.y, $"UI診断 {res.x}x{res.y}");
        waitFrames = 20; // レイアウト・SafeArea の Update が反映されるまで待つ
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        if (--waitFrames > 0) { GameViewSizeTool.Repaint(); return; }

        Vector2Int res = Resolutions[step];
        string report = BuildReport(out int outside);
        string path = Write(report, $"Sweep_{res.x}x{res.y}");
        ScreenCapture.CaptureScreenshot(Path.ChangeExtension(path, ".png"));
        summary.AppendLine($"{res.x}x{res.y} (実Screen={Screen.width}x{Screen.height})\t{outside}\t{path}");

        step++;
        if (step < Resolutions.Length) { ApplyStep(); return; }

        EditorApplication.update -= Tick;
        if (originalSizeIndex >= 0) GameViewSizeTool.SetIndex(originalSizeIndex);
        string summaryPath = Write(summary.ToString(), "Sweep_Summary");
        Debug.Log($"[UILayoutDiagnostics] 4解像度の検証が完了しました。\n{summary}\n一覧: {summaryPath}");
    }

    /// <summary>Game View の解像度を切り替える（UnityEditor 内部 API をリフレクションで使用）</summary>
    internal static class GameViewSizeTool
    {
        private static readonly Assembly EditorAssembly = typeof(Editor).Assembly;
        private static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");
        private static readonly Type SizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
        private static readonly Type SizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
        private static readonly Type SizeKindType = EditorAssembly.GetType("UnityEditor.GameViewSizeType");

        private static EditorWindow GameView()
        {
            if (GameViewType == null) return null;
            return Resources.FindObjectsOfTypeAll(GameViewType).FirstOrDefault() as EditorWindow;
        }

        private static object CurrentGroup()
        {
            object instance = typeof(ScriptableSingleton<>).MakeGenericType(SizesType).GetProperty("instance").GetValue(null);
            return SizesType.GetProperty("currentGroup").GetValue(instance);
        }

        public static int CurrentIndex()
        {
            EditorWindow view = GameView();
            if (view == null) return -1;
            PropertyInfo prop = GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop != null ? (int)prop.GetValue(view) : -1;
        }

        public static void SetFixedResolution(int width, int height, string label)
        {
            object group = CurrentGroup();
            Type groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                object size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)SizeType.GetProperty("width").GetValue(size) == width
                    && (int)SizeType.GetProperty("height").GetValue(size) == height
                    && SizeType.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution")
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                object fixedKind = Enum.Parse(SizeKindType, "FixedResolution");
                object newSize = Activator.CreateInstance(SizeType, fixedKind, width, height, label);
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            SetIndex(index);
        }

        public static void SetIndex(int index)
        {
            EditorWindow view = GameView();
            if (view == null) return;
            PropertyInfo prop = GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite) prop.SetValue(view, index);
            else GameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(view, new object[] { index, null });
            view.Repaint();
        }

        public static void Repaint()
        {
            EditorWindow view = GameView();
            if (view != null) view.Repaint();
        }
    }
}
