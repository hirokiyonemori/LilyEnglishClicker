using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Idle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 【診断用】Logs/EnemyEncounterProbe/build.txt → EnemyEncounterBuilder.BuildAll()
///           Logs/EnemyEncounterProbe/test.txt  → Play で通常敵とボスを 4 解像度で撮影し、接地・HUD・配置を検証
/// </summary>
[InitializeOnLoad]
public static class EnemyEncounterProbe
{
    private const string Dir = "Logs/EnemyEncounterProbe";
    private const string K = "EnemyEncounterProbe.";
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1080, 1920), new Vector2Int(720, 1280), new Vector2Int(1170, 2532), new Vector2Int(1080, 2340),
    };
    private static readonly Func<bool>[] Actions = { Prepare, EnterPlay, WaitReady, ShotsNormal, ForceBoss, ShotsBoss, CheckHpBar, Finish };
    private static double nextPoll;

    static EnemyEncounterProbe()
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
    private static void Log(string line) => File.AppendAllText(Path.Combine(Dir, "result.tsv"), $"{DateTime.Now:HH:mm:ss}\t{line}\n");
    private static void Check(string name, bool ok, string detail = "") => Log($"{(ok ? "PASS" : "FAIL")}\t{name}\t{detail}");

    private static void Tick()
    {
        if (Index < 0)
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (Take("build.txt"))
            {
                string err;
                try { err = EnemyEncounterBuilder.BuildAll(); } catch (Exception e) { err = e.ToString(); }
                File.WriteAllText(Path.Combine(Dir, "build_status.txt"), err == null ? $"DONE\t{DateTime.Now:o}" : $"ERROR\t{err}");
                return;
            }
            if (Take("quit.txt")) { EditorApplication.Exit(0); return; } // 検証のために起動した Unity を閉じる（保存はしない）
            if (Take("test.txt"))
            {
                File.Delete(Path.Combine(Dir, "result.tsv"));
                foreach (string f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);
                Log("INFO\t開始");
                Index = 0; Sub = 0; Wait(0);
                SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            }
            return;
        }
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(K + "StepStart", 0f) > 90)
        {
            Check($"タイムアウト(step {Index})", false);
            // 一度だけ記録して後片付けへ。経過時間を戻さないと毎フレーム再判定されて止まらなくなる
            SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
            if (Index == Actions.Length - 1) { Index = -1; return; }
            Index = Actions.Length - 1;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            return;
        }
        if (Waiting) { UILayoutDiagnostics.GameViewSizeTool.Repaint(); return; }
        bool done;
        try { done = Actions[Index](); }
        catch (Exception e) { Check($"例外(step {Index})", false, e.ToString().Replace('\n', ' ')); done = true; }
        if (!done) return;
        Sub = 0;
        Index = Index + 1;
        SessionState.SetFloat(K + "StepStart", (float)EditorApplication.timeSinceStartup);
        if (Index >= Actions.Length) Index = -1;
    }

    private static bool Take(string name)
    {
        string p = Path.Combine(Dir, name);
        if (!File.Exists(p)) return false;
        File.Delete(p);
        return true;
    }

    private static bool Prepare()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != EnemyEncounterBuilder.ScenePath)
        {
            if (active.isDirty) { Check("Game シーンを開けない（未保存の変更あり）", false, active.path); Index = Actions.Length - 1; return false; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EnemyEncounterBuilder.ScenePath);
        }
        SessionState.SetInt(K + "OrigSize", UILayoutDiagnostics.GameViewSizeTool.CurrentIndex());
        SetResolution(0);
        return true;
    }

    private static bool EnterPlay()
    {
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        return true;
    }

    private static bool WaitReady()
    {
        if (!EditorApplication.isPlaying || !StageProbeSupport.EnsureBattle() || Enemy() == null) return false;
        if (Sub == 0) { Sub = 1; Wait(2.0); return false; }
        return true;
    }

    private static bool ShotsNormal() => Shots("normal");
    private static bool ShotsBoss() => Shots("boss");

    private static bool Shots(string tag)
    {
        int i = Sub / 2;
        if (i >= Resolutions.Length) { SetResolution(0); return true; }
        if (Sub % 2 == 0) { SetResolution(i); Sub = Sub + 1; return false; }
        Verify($"{tag} {Label(i)}");
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, $"{tag}_{Label(i)}.png"));
        Wait(1.0);
        Sub = Sub + 1;
        return false;
    }

    /// <summary>ボスが出る状態（最後の1体）にして敵を出し直す。撃破処理は通さないのでスコアや報酬は増えない</summary>
    private static bool ForceBoss()
    {
        Idle.GameManager gm = UnityEngine.Object.FindFirstObjectByType<Idle.GameManager>();
        EnemyController current = Enemy();
        FieldInfo field = typeof(Idle.GameManager).GetField("currentEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        gm.currentScore = gm.targetScore - 1;
        // ステージ制では「通常の敵を25体倒した後」にボスが出る（bossPhase）。それを直接立てる
        typeof(Idle.GameManager).GetField("bossPhase", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(gm, true);
        if (current != null) UnityEngine.Object.DestroyImmediate(current.gameObject);
        field.SetValue(gm, null);
        gm.SpawnEnemy();
        EnemyController boss = Enemy();
        Check("ボスを出現させた", boss != null && boss.GetEnemyData() != null && boss.GetEnemyData().tier == 5, boss != null && boss.GetEnemyData() != null ? boss.GetEnemyData().displayName : "-");
        Wait(1.0);
        return true;
    }

    private static bool CheckHpBar()
    {
        EnemyController e = Enemy();
        int max = e.GetMaxHp();
        e.TakeDamage(1);
        TMP_Text hp = e.transform.Find("Body/EnemyHUD/EnemyHpText").GetComponent<TMP_Text>();
        RectTransform fill = (RectTransform)e.transform.Find("Body/EnemyHUD/EnemyHpBar/Fill");
        Check("ダメージで HP 数値が減る", hp.text == $"{max - 1:N0}/{max:N0}", hp.text);
        Check("ダメージで HP ゲージが減る", Mathf.Abs(fill.anchorMax.x - (float)(max - 1) / max) < 0.001f, $"{fill.anchorMax.x:0.###}");
        return true;
    }

    private static bool Finish()
    {
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return false; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        int orig = SessionState.GetInt(K + "OrigSize", -1);
        if (orig >= 0) UILayoutDiagnostics.GameViewSizeTool.SetIndex(orig);
        string[] lines = File.ReadAllLines(Path.Combine(Dir, "result.tsv"));
        Log($"INFO\t完了 PASS={lines.Count(l => l.Contains("\tPASS\t"))} FAIL={lines.Count(l => l.Contains("\tFAIL\t"))}");
        return true;
    }

    // ─── 検証 ───────────────────────────────────────────────────
    private static void Verify(string stage)
    {
        EnemyController e = Enemy();
        Transform t = e.transform;
        RectTransform body = (RectTransform)t.Find("Body");
        RectTransform hud = (RectTransform)t.Find("Body/EnemyHUD");
        RectTransform shadow = (RectTransform)t.Find("Shadow");
        RectTransform player = All<RectTransform>().First(r => r.name == "Player" && r.parent != null && r.parent.name == "PnlSafe");
        Canvas canvas = player.GetComponentInParent<Canvas>().rootCanvas;
        Rect view = canvas.pixelRect;

        Rect b = UILayoutProbeRuntime.ScreenRect(body);
        Rect h = UILayoutProbeRuntime.ScreenRect(hud);
        Rect s = UILayoutProbeRuntime.ScreenRect(shadow);
        Rect p = UILayoutProbeRuntime.ScreenRect(player);
        Image lily = player.GetComponent<Image>();
        Rect lv = EnemyEncounterBuilder.VisibleRect(lily.sprite);
        Rect lilyVisible = Rect.MinMaxRect(p.xMin + lv.xMin * p.width, p.yMin + lv.yMin * p.height, p.xMin + lv.xMax * p.width, p.yMin + lv.yMax * p.height);
        float unit = view.width / 1080f; // Match=0 なので 1 単位 = view.width/1080 px

        Log($"LAYOUT\t{stage}\tview={view.width}x{view.height}\tenemy={e.GetEnemyData()?.displayName}\tbody={F(b)}\thud={F(h)}\tshadow={F(s)}\tlily={F(lilyVisible)}");
        bool inside(Rect r) => r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= view.width + 0.5f && r.yMax <= view.height + 0.5f;
        Check($"{stage}: 旧テキスト（英単語・黄色の読み・HP）の単独表示が無い", t.Find("Text_English") == null && t.Find("Text_Reading") == null && t.Find("Text_HP") == null);
        Check($"{stage}: 敵の足元が Lily の足元と同じ高さ", Mathf.Abs(b.yMin - lilyVisible.yMin) <= 3f * unit + 0.5f, $"enemy={b.yMin:0} lily={lilyVisible.yMin:0}");
        Check($"{stage}: 影が足元の中心にある", Mathf.Abs(s.center.y - b.yMin) <= 1.5f && Mathf.Abs(s.center.x - b.center.x) <= 1.5f, $"shadow=({s.center.x:0},{s.center.y:0}) foot=({b.center.x:0},{b.yMin:0})");
        Check($"{stage}: Enemy HUD が敵の真上にある", h.yMin >= b.yMax - 0.5f && Mathf.Abs(h.center.x - b.center.x) <= 1.5f, $"hud.yMin={h.yMin:0} body.yMax={b.yMax:0}");
        Check($"{stage}: Enemy HUD がコンパクト（幅 320 単位以下）", h.width <= 320f * unit + 0.5f, $"{h.width / unit:0}単位");
        Check($"{stage}: 敵が Lily の右側で重ならない", b.xMin >= lilyVisible.xMax, $"enemy.xMin={b.xMin:0} lily.xMax={lilyVisible.xMax:0}");
        Check($"{stage}: 敵と Enemy HUD が画面内", inside(b) && inside(h), $"body={F(b)} hud={F(h)}");
        Check($"{stage}: 敵が以前（高さ約105）より大きい", b.height / unit >= 250f, $"見た目の高さ {b.height / unit:0}単位");
        TMP_Text name = hud.Find("EnemyNameText").GetComponent<TMP_Text>();
        Check($"{stage}: HUD に英単語（敵の名前）が出ている", !string.IsNullOrEmpty(name.text) && name.text != "enemy", name.text);
    }

    private static EnemyController Enemy() => All<EnemyController>().FirstOrDefault();
    private static string F(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";
    private static string Label(int i) => $"{Resolutions[i].x}x{Resolutions[i].y}";

    private static void SetResolution(int i)
    {
        UILayoutDiagnostics.GameViewSizeTool.SetFixedResolution(Resolutions[i].x, Resolutions[i].y, $"UI診断 {Label(i)}");
        Wait(1.5);
    }

    private static System.Collections.Generic.List<T> All<T>() where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return list;
    }
}
