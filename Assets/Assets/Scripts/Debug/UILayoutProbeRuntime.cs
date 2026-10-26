#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【Editor専用・診断用】Play 開始直後の各タイミングで主要 UI の RectTransform を記録する。
/// UILayoutProbe（Editor）から有効化された Play セッションでだけ動き、ビルドには含まれない。
/// </summary>
public static class UILayoutProbeRuntime
{
    public const string Dir = "Logs/UILayoutProbe"; // Temp は Unity 終了時に消えるため Logs に残す
    public const string ActiveKey = "UILayoutProbe.Active";
    public static bool TimelineDone;

    public static readonly (string label, string suffix)[] MainTargets =
    {
        ("GOLD背景", "PnlSafe/Text_Gold_BG"), ("GOLDアイコン", "PnlSafe/Icon_Gold"), ("GOLD文字", "PnlSafe/Text_Gold"),
        ("SCORE背景", "PnlSafe/Text_Score_BG"), ("SCOREアイコン", "PnlSafe/Icon_Score"), ("SCORE文字", "PnlSafe/Text_Score"),
        ("Lily", "PnlSafe/Player"), ("下部HUD", "PnlSafe/StatusWindow"), ("本編背景", "PnlSafe/Background"),
        ("本編SafeArea", "PnlTop/PnlSafe"), ("QuestSafeArea", "QuestUIRoot/LaunchSafeArea"), ("入口ダイアログ", "DialogSafeArea/QuestDialogCard"),
    };

    public static readonly (string label, string suffix)[] QuestTargets =
    {
        ("Quest内SafeArea", "QuestUIRoot/QuestUIRoot/SafeArea"), ("選択パネル", "QuestChoicePanel/Content"),
        ("章ヘッダー", "QuestChoicePanel/Content/ChapterHeader"), ("左カード", "QuestChoicePanel/Content/LeftCard"),
        ("右カード", "QuestChoicePanel/Content/RightCard"), ("Lily案内画像", "QuestChoicePanel/Content/LilyGuideImage"),
        ("Pipヒント", "PipHintController/PipHint"),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        TimelineDone = false;
        if (!UnityEditor.SessionState.GetBool(ActiveKey, false)) return;
        Application.runInBackground = true;
        Snapshot("P1_AfterSceneLoad(Awake/OnEnable後・Start前)", MainTargets);
        var go = new GameObject("[UILayoutProbe]") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<Timeline>();
    }

    private sealed class Timeline : MonoBehaviour
    {
        private int frame;
        private bool renderSnapshotTaken;

        private void OnEnable() { Canvas.willRenderCanvases += OnWillRenderCanvases; }
        private void OnDisable() { Canvas.willRenderCanvases -= OnWillRenderCanvases; }

        private void LateUpdate()
        {
            frame++;
            if (frame == 1) Snapshot("P2_初回LateUpdate(全Start・Update後)", MainTargets);
        }

        private void OnWillRenderCanvases()
        {
            if (renderSnapshotTaken || frame < 1) return;
            renderSnapshotTaken = true;
            Snapshot("P3_初回Canvas描画直前", MainTargets);
        }

        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            Snapshot("P4_初回フレーム描画後", MainTargets);
            for (int i = 0; i < 10; i++) yield return null;
            Snapshot("P5_約10フレーム後", MainTargets);
            for (int i = 0; i < 50; i++) yield return null;
            Snapshot("P6_約60フレーム後", MainTargets);
            TimelineDone = true;
        }
    }

    /// <summary>主要 UI の RectTransform と画面上の矩形を Logs/UILayoutProbe/timeline.tsv に追記する</summary>
    public static void Snapshot(string stage, (string label, string suffix)[] targets)
    {
        Directory.CreateDirectory(Dir);
        var sb = new StringBuilder();
        Rect safe = Screen.safeArea;
        Rect valid = SafeAreaUtility.Validate(safe, Screen.width, Screen.height, logWarning: false);
        sb.AppendLine($"#STAGE\t{stage}\tframe={Time.frameCount}\tplaying={Application.isPlaying}\tscreen={Screen.width}x{Screen.height}\tsafeArea={R(safe)}\tvalidated={R(valid)}");
        foreach (Canvas c in All<Canvas>().Where(c => c.isRootCanvas))
        {
            CanvasScaler s = c.GetComponent<CanvasScaler>();
            var rt = (RectTransform)c.transform;
            sb.AppendLine($"#CANVAS\t{stage}\t{PathOf(c.transform)}\tmode={c.renderMode}\tmatch={(s != null ? s.matchWidthOrHeight.ToString() : "-")}\tscaleFactor={c.scaleFactor:0.####}\tsize={V(rt.rect.size)}\tactive={c.gameObject.activeInHierarchy}");
        }
        List<RectTransform> all = All<RectTransform>();
        foreach (var (label, suffix) in targets)
        {
            RectTransform rt = all.FirstOrDefault(r => PathOf(r).EndsWith(suffix, System.StringComparison.Ordinal));
            if (rt == null) { sb.AppendLine($"{stage}\t{label}\tMISSING\t{suffix}"); continue; }
            Rect sr = ScreenRect(rt);
            bool inside = sr.xMin >= -0.5f && sr.yMin >= -0.5f && sr.xMax <= Screen.width + 0.5f && sr.yMax <= Screen.height + 0.5f;
            string extra = "";
            if (suffix.EndsWith("Background"))
            {
                AspectRatioFitter f = rt.GetComponent<AspectRatioFitter>();
                extra = f != null ? $"fitter={f.aspectMode}:{f.aspectRatio:0.####}" : "fitter=none";
            }
            var pc = rt.GetComponent("PlayerController");
            if (pc != null)
            {
                FieldInfo fi = pc.GetType().GetField("originalPosition", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null) extra = $"controllerBase={fi.GetValue(pc)}";
            }
            sb.AppendLine($"{stage}\t{label}\t{(rt.gameObject.activeInHierarchy ? "active" : "inactive")}\tamin={V(rt.anchorMin)}\tamax={V(rt.anchorMax)}\tpivot={V(rt.pivot)}\tpos={V(rt.anchoredPosition)}\tsize={V(rt.sizeDelta)}\toffMin={V(rt.offsetMin)}\toffMax={V(rt.offsetMax)}\tlocal={V(rt.localPosition)}\trect={V(rt.rect.size)}\tscreen={R(sr)}\t{(inside ? "IN" : "OUT")}\t{extra}");
        }
        File.AppendAllText(Path.Combine(Dir, "timeline.tsv"), sb.ToString());
    }

    public static Rect ScreenRect(RectTransform rt)
    {
        Canvas canvas = rt.GetComponentInParent<Canvas>(true);
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
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

    private static List<T> All<T>() where T : Component
    {
        var list = new List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
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

    private static string V(Vector2 v) => $"({v.x:0.###},{v.y:0.###})";
    private static string V(Vector3 v) => $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";
    private static string R(Rect r) => $"({r.xMin:0.#},{r.yMin:0.#})-({r.xMax:0.#},{r.yMax:0.#})";
}
#endif
