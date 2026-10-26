using System.Collections.Generic;
using System.IO;
using System.Linq;
using Idle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 本編の敵表示を「RPG のエンカウント」として組み立てる。
/// 1) 敵スプライトの「描かれている範囲」を解析して EnemySpriteBounds に保存
/// 2) EnemyPrefab を 影 / 接地した体 / 体の上の小さな Enemy HUD（名前＋HP）の構成に組み直す
///    （既存 Prefab の中身を入れ替えるので、GameManager.enemyPrefab などの参照は切れない）
/// 3) Game シーンの EnemySpawner を Lily の足元と同じ高さ・左右対称の位置へ置く（Lily の配置から計算）
/// </summary>
public static class EnemyEncounterBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Enemy/EnemyPrefab.prefab";
    public const string BoundsPath = "Assets/Data/Enemies/EnemySpriteBounds.asset";
    public const string DatabasePath = "Assets/Data/Enemies/EnemyDatabase.asset";
    public const string ScenePath = "Assets/Scenes/Game.unity";

    public static readonly Vector2 HudSize = new Vector2(300f, 88f);
    public const float HudGap = 14f;
    private const byte AlphaThreshold = 24;

    private static readonly Color Cream = new Color32(0xF5, 0xED, 0xD8, 0xFF);
    private static readonly Color Muted = new Color32(0xB8, 0xCA, 0xDA, 0xFF);
    private static readonly Color Gold = new Color32(0xDA, 0xC1, 0x8B, 0xFF);

    [MenuItem("LilyClicker/敵表示/エンカウント表示を再構築（Prefab・境界表・出現位置）")]
    public static void BuildAllFromMenu()
    {
        string error = BuildAll();
        if (error != null) EditorUtility.DisplayDialog("敵表示の再構築", error, "OK");
    }

    /// <summary>batchmode 用: Unity -batchmode -executeMethod EnemyEncounterBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        string error = BuildAll();
        if (error != null) Debug.LogError(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    /// <summary>成功なら null、失敗・中止ならその理由を返す</summary>
    public static string BuildAll()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
            return "Game シーンに未保存の変更があるため中止しました。保存してから再実行してください。";

        EnemySpriteBounds bounds = BuildBoundsTable();
        BuildPrefab(bounds);

        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }
        string layoutError = LayoutSpawner(scene);
        if (layoutError == null) EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        Debug.Log(layoutError ?? "[EnemyEncounterBuilder] 敵表示を再構築しました。");
        return layoutError;
    }

    // ─── 1) 描かれている範囲の解析 ─────────────────────────────
    private static EnemySpriteBounds BuildBoundsTable()
    {
        var sprites = new List<Sprite>();
        EnemyDatabase db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(DatabasePath);
        if (db != null) sprites.AddRange(db.enemies.Where(e => e != null && e.sprite != null).Select(e => e.sprite));
        Sprite fallback = CurrentPrefabSprite();
        if (fallback != null) sprites.Add(fallback);

        EnemySpriteBounds table = AssetDatabase.LoadAssetAtPath<EnemySpriteBounds>(BoundsPath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<EnemySpriteBounds>();
            AssetDatabase.CreateAsset(table, BoundsPath);
        }
        table.entries.Clear();
        foreach (Sprite s in sprites.Distinct())
            table.entries.Add(new EnemySpriteBounds.Entry { sprite = s, visible = VisibleRect(s) });
        EditorUtility.SetDirty(table);
        return table;
    }

    /// <summary>スプライト内で不透明な範囲（0〜1・下原点）。PNG を直接読むので Read/Write 設定は変えない</summary>
    public static Rect VisibleRect(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(path))) return new Rect(0, 0, 1, 1);
            // インポート時の縮小を考慮して、スプライトの範囲を元画像の座標へ換算する
            float sx = (float)tex.width / sprite.texture.width, sy = (float)tex.height / sprite.texture.height;
            RectInt r = new RectInt(Mathf.RoundToInt(sprite.rect.x * sx), Mathf.RoundToInt(sprite.rect.y * sy),
                                    Mathf.RoundToInt(sprite.rect.width * sx), Mathf.RoundToInt(sprite.rect.height * sy));
            Color32[] px = tex.GetPixels32();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    if (px[y * tex.width + x].a > AlphaThreshold)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return new Rect(0, 0, 1, 1);
            return Rect.MinMaxRect((float)(minX - r.xMin) / r.width, (float)(minY - r.yMin) / r.height,
                                   (float)(maxX + 1 - r.xMin) / r.width, (float)(maxY + 1 - r.yMin) / r.height);
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }

    private static Sprite CurrentPrefabSprite()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return null;
        Transform sprite = prefab.transform.Find("Body/Sprite");
        Image img = sprite != null ? sprite.GetComponent<Image>() : prefab.GetComponent<Image>();
        return img != null ? img.sprite : null;
    }

    // ─── 2) Prefab の組み直し ───────────────────────────────────
    private static void BuildPrefab(EnemySpriteBounds bounds)
    {
        Sprite defaultSprite = CurrentPrefabSprite();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(QuestTMPStabilizer.QuestFontPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (string name in new[] { "Text_English", "Text_Reading", "Text_HP", "Shadow", "Body" })
            {
                Transform old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            Image rootImage = root.GetComponent<Image>();
            if (rootImage != null) Object.DestroyImmediate(rootImage);

            // 原点＝足元（地面）。親（EnemySpawner）の中心に置く
            var rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(.5f, .5f);
            rootRt.pivot = new Vector2(.5f, 0f);
            rootRt.sizeDelta = Vector2.zero;
            rootRt.anchoredPosition = Vector2.zero;

            RectTransform shadow = Node(root.transform, "Shadow", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(260f, 52f), Vector2.zero);
            var ellipse = shadow.gameObject.AddComponent<SoftEllipseGraphic>();
            ellipse.color = new Color(0.02f, 0.03f, 0.05f, 1f);
            ellipse.centerAlpha = 0.38f;
            ellipse.solidRadius = 0.3f;
            ellipse.raycastTarget = false;

            RectTransform body = Node(root.transform, "Body", new Vector2(.5f, 0f), new Vector2(.5f, .5f), new Vector2(300f, 300f), Vector2.zero);
            RectTransform spriteRt = Node(body, "Sprite", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, Vector2.zero);
            spriteRt.anchorMin = Vector2.zero; spriteRt.anchorMax = Vector2.one;
            var image = spriteRt.gameObject.AddComponent<Image>();
            image.sprite = defaultSprite;
            image.preserveAspect = false;
            image.raycastTarget = true;

            // 体の上の小さな Enemy HUD（名前＝英単語＋読み、HP ゲージ＋数値）。背景を隠しすぎない半透明。
            // 本編下部の Lily HUD にも NameText / HpText があるため、名前で探すツールが取り違えないよう Enemy を付ける
            RectTransform hud = Node(body, "EnemyHUD", new Vector2(.5f, 0f), new Vector2(.5f, 1f), HudSize, new Vector2(0f, HudGap));
            var plate = hud.gameObject.AddComponent<QuestSurfaceGraphic>();
            plate.topColor = new Color(0.10f, 0.18f, 0.28f, 0.74f);
            plate.bottomColor = new Color(0.05f, 0.10f, 0.18f, 0.80f);
            plate.borderColor = new Color(Gold.r, Gold.g, Gold.b, 0.65f);
            plate.cornerRadius = 20f;
            plate.borderWidth = 2f;
            plate.raycastTarget = false;

            TextMeshProUGUI nameText = Text(hud, "EnemyNameText", font, "enemy", 38f, Cream, new Vector2(.05f, .44f), new Vector2(.95f, .97f), TextAlignmentOptions.Center);
            RectTransform bar = Stretch(hud, "EnemyHpBar", new Vector2(.07f, .17f), new Vector2(.68f, .37f));
            var barBg = bar.gameObject.AddComponent<QuestSurfaceGraphic>();
            barBg.topColor = barBg.bottomColor = new Color(0.02f, 0.05f, 0.10f, 0.92f);
            barBg.borderColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            barBg.cornerRadius = 7f; barBg.borderWidth = 1.5f; barBg.raycastTarget = false;
            RectTransform fill = Stretch(bar, "Fill", Vector2.zero, Vector2.one);
            fill.offsetMin = new Vector2(3f, 3f); fill.offsetMax = new Vector2(-3f, -3f);
            var fillFace = fill.gameObject.AddComponent<QuestSurfaceGraphic>();
            fillFace.topColor = new Color32(0xF0, 0x6A, 0x55, 0xFF);
            fillFace.bottomColor = new Color32(0xC8, 0x3F, 0x3A, 0xFF);
            fillFace.cornerRadius = 5f; fillFace.borderWidth = 0f; fillFace.raycastTarget = false;
            TextMeshProUGUI hpText = Text(hud, "EnemyHpText", font, "8/8", 24f, Muted, new Vector2(.70f, .06f), new Vector2(.95f, .48f), TextAlignmentOptions.Right);

            Button button = root.GetComponent<Button>();
            if (button != null)
            {
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None; // 押下時の色変化がダメージ演出と競合しないように
            }

            EnemyController controller = root.GetComponent<EnemyController>();
            var so = new SerializedObject(controller);
            so.FindProperty("englishText").objectReferenceValue = nameText;
            so.FindProperty("readingText").objectReferenceValue = null;
            so.FindProperty("hpText").objectReferenceValue = hpText;
            so.FindProperty("hpFill").objectReferenceValue = fill;
            so.FindProperty("enemyImage").objectReferenceValue = image;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.FindProperty("spriteBounds").objectReferenceValue = bounds;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RectTransform Node(Transform parent, string name, Vector2 pivot, Vector2 anchor, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    private static RectTransform Stretch(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, string value, float size, Color color, Vector2 min, Vector2 max, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = Stretch(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = value;
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.6f;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    // ─── 3) 出現位置（Lily と左右対称・同じ地面） ───────────────
    private static string LayoutSpawner(Scene scene)
    {
        RectTransform spawner = null, player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(rt, null);
                if (path.EndsWith("PnlSafe/EnemySpawner")) spawner = rt;
                if (path.EndsWith("PnlSafe/Player")) player = rt;
            }
        if (spawner == null || player == null) return "PnlSafe/EnemySpawner または PnlSafe/Player が見つかりません。";
        if (player.parent != spawner.parent) return "EnemySpawner と Player の親が異なるため配置を計算できません。";

        // Lily の「見えている足元」: 画像の下端 + 透明な余白
        Image lilyImage = player.GetComponent<Image>();
        Rect visible = lilyImage != null && lilyImage.sprite != null ? VisibleRect(lilyImage.sprite) : new Rect(0, 0, 1, 1);
        float footFromPivot = -player.pivot.y * player.sizeDelta.y + visible.yMin * player.sizeDelta.y;
        float groundY = player.anchoredPosition.y + footFromPivot; // 親の縦アンカー基準
        float lilyCenterX = player.anchoredPosition.x + (visible.center.x - player.pivot.x) * player.sizeDelta.x;

        Undo.RecordObject(spawner, "敵の出現位置");
        // 右端から Lily と同じ距離に置く（左右対称）。縦は Lily と同じアンカー＝同じ地面
        spawner.anchorMin = spawner.anchorMax = new Vector2(1f - player.anchorMin.x, player.anchorMin.y);
        spawner.pivot = new Vector2(.5f, .5f);
        spawner.sizeDelta = Vector2.zero;
        spawner.anchoredPosition = new Vector2(-lilyCenterX, groundY);
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        return null;
    }
}
