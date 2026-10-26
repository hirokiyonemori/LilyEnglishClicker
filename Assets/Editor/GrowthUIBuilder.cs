using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LilyEnglish.Growth;
using LilyEnglish.Growth.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 強化システム（Growth）の組み立て。
/// 1) データ（Lily・固有スキル・ショップの永続強化）を Assets/Data/Growth に作る（既にあれば数値は上書きしない）
/// 2) 強化行プレハブと GrowthUIRoot プレハブ（キャラ詳細・強化ショップ）を生成する
/// 3) Game シーンに GrowthService と GrowthUIRoot を置き、本編 HUD（顔アイコン・強化ボタン）を再構築する
/// 見た目は Quest UI と同じ紺・金・クリーム。Prefab を手で直さず、このビルダーを直して再実行する。
/// </summary>
public static class GrowthUIBuilder
{
    public const string DataDir = "Assets/Data/Growth";
    public const string PrefabDir = "Assets/Assets/Prefabs/Growth";
    public const string RowPrefabPath = PrefabDir + "/UpgradeRow.prefab";
    public const string RootPrefabPath = PrefabDir + "/GrowthUIRoot.prefab";
    public const string ScenePath = "Assets/Scenes/Game.unity";
    private const string GemSpritePath = "Assets/Image/UI/Gem.png";
    private const string CoinSpritePath = "Assets/Image/UI/Coin.png";
    private const string StarSpritePath = "Assets/Image/UI/Star.png";
    private const string LilyFullBodyPath = "Assets/Image/Lily/Lily_Idle.png";
    private const string LilyFacePath = "Assets/Image/Lily/Face/Lily_Face_Idle.png";

    private static readonly Vector2 CardSize = new Vector2(960f, 1560f);
    private static readonly Color Cream = Hex("F5EDD8");
    private static readonly Color Gold = Hex("DAC18B");
    private static readonly Color Muted = Hex("B8CADA");
    private static readonly Color Ink = Hex("172D43");
    private static readonly Color Inset = new Color(0.03f, 0.07f, 0.13f, 0.78f);
    private static TMP_FontAsset font;
    private static Sprite gem, coin, star;

    [MenuItem("LilyClicker/強化システム/データ・画面・HUDを再構築（Gameシーン）")]
    public static void BuildFromMenu()
    {
        string error = BuildAll();
        if (error != null) EditorUtility.DisplayDialog("強化システムの再構築", error, "OK");
    }

    /// <summary>batchmode 用: Unity -batchmode -executeMethod GrowthUIBuilder.BuildBatch</summary>
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

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(QuestTMPStabilizer.QuestFontPath);
        if (font == null) return "TMP フォントが見つかりません: " + QuestTMPStabilizer.QuestFontPath;
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(PrefabDir);

        gem = EnsureGemSprite();
        coin = SpriteAt(CoinSpritePath);
        star = SpriteAt(StarSpritePath);

        CharacterDefinition lily = EnsureData(out List<PartyUpgradeDefinition> upgrades);
        GameObject rowPrefab = BuildRowPrefab();
        GameObject rootPrefab = BuildRootPrefab(rowPrefab);

        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }
        InstallIntoScene(scene, lily, upgrades, rootPrefab);
        LilyStatusHUDBuilder.Build(scene); // 顔アイコン（キャラ詳細）と「強化」ボタン（ショップ）を HUD に足す
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[GrowthUIBuilder] 強化システムのデータ・画面・HUD を再構築しました。");
        return null;
    }

    // ─── 1) データ ──────────────────────────────────────────────
    private static CharacterDefinition EnsureData(out List<PartyUpgradeDefinition> upgrades)
    {
        SkillDefinition skill = Ensure<SkillDefinition>("Skill_SpellStar", s =>
        {
            s.skillName = "スペルスター";
            s.descriptionFormat = "冒険の攻撃で\n攻撃力の{0}%の魔法ダメージを与える";
            s.baseMultiplierPercent = 200;
            s.multiplierPerLevel = 20;
            s.maxLevel = 50;
            s.costBase = 50;
            s.costGrowthRate = 1.15f;
            s.requireCorrectAnswer = false;
        });
        if (skill.icon == null) { skill.icon = star; EditorUtility.SetDirty(skill); }

        CharacterDefinition lily = Ensure<CharacterDefinition>("Character_Lily", c =>
        {
            c.characterId = "lily";
            c.displayName = "リリィ";
            c.roleName = "見習い魔法使い";
        });
        Sprite face = SpriteAt(LilyFacePath);
        if (lily.portrait == null) lily.portrait = face;
        if (lily.tabIcon == null) lily.tabIcon = face;
        if (lily.fullBody == null) lily.fullBody = SpriteAt(LilyFullBodyPath);
        if (lily.skill == null) lily.skill = skill;
        EditorUtility.SetDirty(lily);

        // ★＝本編ゴールドで買える強化を上に、💎の強化を下に並べる
        upgrades = new List<PartyUpgradeDefinition>
        {
            Ensure<PartyUpgradeDefinition>("Upgrade_Wand", u => Upgrade(u, "wand", "魔法の杖みがき",
                "強化するとリリィの攻撃力が5%アップ", Currency.Star, 300, 1.35f, 30, new UpgradeEffect { targetCharacterId = "lily", atkPercent = 5 })),
            Ensure<PartyUpgradeDefinition>("Upgrade_Cloak", u => Upgrade(u, "cloak", "まもりのマント",
                "強化するとリリィのHPが5%アップ", Currency.Star, 200, 1.3f, 30, new UpgradeEffect { targetCharacterId = "lily", hpPercent = 5 })),
            Ensure<PartyUpgradeDefinition>("Upgrade_WordBook", u => Upgrade(u, "wordbook", "魔法の単語帳",
                "リリィの攻撃力が30%、防御力が10%アップ", Currency.Gem, 100, 1f, 1,
                new UpgradeEffect { atkPercent = 10, defPercent = 10 }, new UpgradeEffect { targetCharacterId = "lily", atkPercent = 20 })),
            Ensure<PartyUpgradeDefinition>("Upgrade_Ribbon", u => Upgrade(u, "ribbon", "リリィのリボン",
                "強化するとリリィの防御力が3%アップ", Currency.Gem, 3, 1.5f, 20, new UpgradeEffect { defPercent = 3 })),
        };
        AssetDatabase.SaveAssets();
        return lily;
    }

    private static void Upgrade(PartyUpgradeDefinition u, string id, string name, string description, Currency currency,
                                long cost, float rate, int maxLevel, params UpgradeEffect[] effects)
    {
        u.upgradeId = id;
        u.displayName = name;
        u.description = description;
        u.currency = currency;
        u.costBase = cost;
        u.costGrowthRate = rate;
        u.maxLevel = maxLevel;
        u.effectsPerLevel = effects.ToList();
    }

    /// <summary>無ければ作って初期値を入れる。既にあれば調整済みの数値を尊重して触らない</summary>
    private static T Ensure<T>(string name, Action<T> init) where T : ScriptableObject
    {
        string path = $"{DataDir}/{name}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        init(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // ─── 💎 アイコン（画像素材が無いので描いて保存する） ───────────
    private static Sprite EnsureGemSprite()
    {
        Sprite existing = SpriteAt(GemSpritePath);
        if (existing != null) return existing;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var outline = new[] { new Vector2(.30f, .80f), new Vector2(.70f, .80f), new Vector2(.92f, .58f), new Vector2(.50f, .10f), new Vector2(.08f, .58f) };
        var crown = new[] { new Vector2(.30f, .80f), new Vector2(.70f, .80f), new Vector2(.92f, .58f), new Vector2(.08f, .58f) };
        Color edge = new Color(0.86f, 0.98f, 1f, 1f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size, (y + .5f) / size);
                Color c = Color.clear;
                if (Inside(p, outline))
                {
                    bool top = Inside(p, crown);
                    float shade = top ? 0.95f : Mathf.Lerp(0.55f, 0.85f, p.y);
                    c = new Color(0.35f * shade, 0.85f * shade, 1f * shade, 1f);
                    if (top && p.x > .42f && p.x < .58f) c = Color.Lerp(c, Color.white, 0.45f); // 上面のハイライト
                    if (DistanceToPolygon(p, outline) < 0.025f) c = edge;
                }
                tex.SetPixel(x, y, c);
            }
        File.WriteAllBytes(GemSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(GemSpritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(GemSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return SpriteAt(GemSpritePath);
    }

    private static bool Inside(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    private static float DistanceToPolygon(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    // ─── 2) 強化行プレハブ ───────────────────────────────────────
    private static GameObject BuildRowPrefab()
    {
        RectTransform row = Rect(null, "UpgradeRow", 0, 0, 1, 1);
        Surface(row, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, 0.55f), 22f, 2f);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 176f;
        layout.minHeight = 176f;

        RectTransform frame = Rect(row, "IconFrame", .025f, .14f, .155f, .86f);
        RectTransform fallback = Rect(frame, "FallbackEmblem", .12f, .12f, .88f, .88f);
        Emblem(fallback, QuestEmblemGraphic.Symbol.Star);
        Image icon = Picture(frame, "Icon", null, 0, 0, 1, 1, true);

        TMP_Text name = Label(row, "NameText", "魔法の杖みがき", 38, Cream, .18f, .56f, .66f, .92f, TextAlignmentOptions.Left);
        RectTransform pill = Rect(row, "BadgePill", .18f, .38f, .45f, .56f);
        Surface(pill, new Color(0.10f, 0.18f, 0.28f, 0.9f), new Color(0.10f, 0.18f, 0.28f, 0.9f), new Color(Gold.r, Gold.g, Gold.b, 0.6f), 14f, 1.5f);
        TMP_Text badge = Label(pill, "BadgeText", "強化+0", 22, Gold, 0, 0, 1, 1);
        TMP_Text desc = Label(row, "DescText", "強化するとリリィの攻撃力が5%アップ", 24, Muted, .18f, .06f, .66f, .37f, TextAlignmentOptions.TopLeft);
        desc.textWrappingMode = TextWrappingModes.Normal;

        Button buy = ActionButton(row, "BuyButton", "強化する", .685f, .12f, .97f, .88f, true, out TMP_Text buyLabel);
        RectTransform labelRt = buyLabel.rectTransform;
        labelRt.anchorMin = new Vector2(.05f, .50f); labelRt.anchorMax = new Vector2(.95f, .94f);
        Image currency = Picture(buy.transform, "CurrencyIcon", coin, .14f, .10f, .36f, .48f, true);
        TMP_Text cost = Label(buy.transform, "CostText", "300", 30, Ink, .38f, .06f, .95f, .50f, TextAlignmentOptions.Left);

        var ui = row.gameObject.AddComponent<PartyUpgradeRowUI>();
        Set(ui, "icon", icon); Set(ui, "nameText", name); Set(ui, "descText", desc); Set(ui, "badgeText", badge);
        Set(ui, "costText", cost); Set(ui, "buttonLabel", buyLabel); Set(ui, "currencyIcon", currency);
        Set(ui, "gemSprite", gem); Set(ui, "starSprite", coin); Set(ui, "expSprite", star);
        Set(ui, "buyButton", buy); Set(ui, "fallbackIcon", fallback.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPrefabPath);
        Object.DestroyImmediate(row.gameObject);
        return prefab;
    }

    // ─── 2) GrowthUIRoot（キャラ詳細・強化ショップ） ──────────────
    private static GameObject BuildRootPrefab(GameObject rowPrefab)
    {
        RectTransform root = Rect(null, "GrowthUIRoot", 0, 0, 1, 1);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105; // 本編 HUD より上、クエスト画面（100）とは同時に開かない
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        GrowthUIView view = root.gameObject.AddComponent<GrowthUIView>();

        BuildDetail(root, view);
        BuildShop(root, view, rowPrefab);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RootPrefabPath);
        Object.DestroyImmediate(root.gameObject);
        return prefab;
    }

    /// <summary>全画面の暗幕と SafeArea 中央のカード。暗幕は背後の本編（タップ攻撃）への入力を遮る</summary>
    private static RectTransform Modal(Transform parent, string name, out GameObject host)
    {
        RectTransform panel = Rect(parent, name, 0, 0, 1, 1);
        host = panel.gameObject;
        Image dim = Picture(panel, "DimOverlay", null, 0, 0, 1, 1, false);
        dim.color = new Color(0.02f, 0.04f, 0.08f, 0.72f);
        dim.raycastTarget = true;
        RectTransform safe = Rect(panel, "SafeArea", 0, 0, 1, 1);
        safe.gameObject.AddComponent<QuestSafeArea>();
        // カードは縦横比（960:1560）を保ったまま余白の内側に収める。高さ固定だと
        // iPad など縦横比の短い画面（1080x1440 相当）で上下がはみ出すため
        RectTransform area = Rect(safe, "CardArea", 0, 0, 1, 1);
        area.offsetMin = new Vector2(40f, 50f);
        area.offsetMax = new Vector2(-40f, -50f);
        RectTransform card = Rect(area, "Card", .5f, .5f, .5f, .5f);
        card.sizeDelta = CardSize;
        var fitter = card.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = CardSize.x / CardSize.y;
        QuestSurfaceGraphic face = card.gameObject.AddComponent<QuestSurfaceGraphic>();
        face.raycastTarget = true;
        return card;
    }

    private static void Wallet(RectTransform card, float y0, float y1)
    {
        RectTransform bar = Rect(card, "WalletBar", .05f, y0, .95f, y1);
        Surface(bar, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 18f, 1.5f);
        TMP_Text Group(string key, Sprite icon, string label, float x0, float x1)
        {
            RectTransform g = Rect(bar, key, x0, 0, x1, 1);
            if (icon != null) Picture(g, "Icon", icon, .02f, .18f, .26f, .82f, true);
            else Label(g, "Icon", label, 28, Gold, .0f, .1f, .30f, .9f);
            return Label(g, "Value", "0", 32, Cream, .30f, .1f, .98f, .9f, TextAlignmentOptions.Left);
        }
        TMP_Text exp = Group("Exp", null, "EXP", .03f, .36f);
        TMP_Text gold = Group("Gold", coin, null, .37f, .68f);
        TMP_Text gemValue = Group("Gem", gem, null, .69f, .98f);
        var ui = bar.gameObject.AddComponent<WalletUI>();
        Set(ui, "expText", exp); Set(ui, "starText", gold); Set(ui, "gemText", gemValue);
    }

    private static void BuildDetail(RectTransform root, GrowthUIView view)
    {
        RectTransform card = Modal(root, "CharacterDetailPanel", out GameObject host);
        var detail = host.AddComponent<CharacterDetailUI>();
        view.detail = detail;

        TMP_Text name = Label(card, "NameText", "リリィ", 54, Cream, .06f, .915f, .62f, .975f, TextAlignmentOptions.Left);
        Label(card, "RoleText", "見習い魔法使い", 26, Muted, .06f, .885f, .62f, .92f, TextAlignmentOptions.Left);
        TMP_Text level = Label(card, "LevelText", "Lv1", 50, Gold, .62f, .895f, .94f, .975f, TextAlignmentOptions.Right);
        Wallet(card, .815f, .87f);

        Image fullBody = Picture(card, "FullBody", SpriteAt(LilyFullBodyPath), .03f, .40f, .47f, .80f, true);
        RectTransform stats = Rect(card, "Stats", .50f, .555f, .95f, .80f);
        Surface(stats, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 18f, 1.5f);
        TMP_Text Stat(string key, string label, float y0, float y1)
        {
            Label(stats, key + "Label", label, 30, Gold, .07f, y0, .40f, y1, TextAlignmentOptions.Left);
            return Label(stats, key + "Value", "0", 46, Cream, .38f, y0, .93f, y1, TextAlignmentOptions.Right);
        }
        TMP_Text hp = Stat("Hp", "HP", .67f, .95f);
        TMP_Text atk = Stat("Atk", "ATK", .37f, .65f);
        TMP_Text def = Stat("Def", "DEF", .07f, .35f);

        Button levelUp = ActionButton(card, "LevelUpButton", "レベルアップ", .50f, .425f, .95f, .53f, true, out TMP_Text levelUpLabel);
        levelUpLabel.rectTransform.anchorMin = new Vector2(.05f, .46f);
        TMP_Text levelUpCost = Label(levelUp.transform, "CostText", "100 EXP", 30, Ink, .05f, .06f, .95f, .48f);

        RectTransform skill = Rect(card, "SkillBox", .05f, .135f, .95f, .385f);
        Surface(skill, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 20f, 1.5f);
        Image skillIcon = Picture(skill, "SkillIcon", star, .03f, .62f, .13f, .92f, true);
        TMP_Text skillName = Label(skill, "SkillName", "スペルスター", 40, Cream, .15f, .64f, .70f, .93f, TextAlignmentOptions.Left);
        TMP_Text skillLevel = Label(skill, "SkillLevel", "Lv1", 36, Gold, .70f, .64f, .96f, .93f, TextAlignmentOptions.Right);
        TMP_Text skillDesc = Label(skill, "SkillDesc", "", 28, Muted, .04f, .33f, .96f, .63f);
        skillDesc.textWrappingMode = TextWrappingModes.Normal;
        Button skillUp = ActionButton(skill, "SkillUpButton", "スキルレベルアップ", .04f, .05f, .96f, .30f, false, out TMP_Text skillUpLabel);
        skillUpLabel.rectTransform.anchorMin = new Vector2(.04f, .05f);
        skillUpLabel.rectTransform.anchorMax = new Vector2(.62f, .95f);
        TMP_Text skillUpCost = Label(skillUp.transform, "CostText", "50 EXP", 30, Gold, .62f, .05f, .96f, .95f, TextAlignmentOptions.Right);

        view.detailToShopButton = ActionButton(card, "ToShopButton", "強化ショップへ", .05f, .03f, .485f, .11f, false, out _);
        Button close = ActionButton(card, "CloseButton", "とじる", .515f, .03f, .95f, .11f, false, out _);

        var feedback = card.gameObject.AddComponent<LevelUpFeedback>();
        Set(feedback, "target", fullBody.rectTransform);

        Set(detail, "fullBodyImage", fullBody); Set(detail, "nameText", name); Set(detail, "levelText", level);
        Set(detail, "hpText", hp); Set(detail, "atkText", atk); Set(detail, "defText", def);
        Set(detail, "levelUpButton", levelUp); Set(detail, "levelUpCostText", levelUpCost);
        Set(detail, "skillIcon", skillIcon); Set(detail, "skillNameText", skillName); Set(detail, "skillLevelText", skillLevel);
        Set(detail, "skillDescText", skillDesc); Set(detail, "skillUpButton", skillUp); Set(detail, "skillUpCostText", skillUpCost);
        Set(detail, "closeButton", close); Set(detail, "feedback", feedback);
        host.SetActive(false);
    }

    private static void BuildShop(RectTransform root, GrowthUIView view, GameObject rowPrefab)
    {
        RectTransform card = Modal(root, "ShopPanel", out GameObject host);
        view.shopRoot = host;
        var shop = host.AddComponent<ShopUI>();

        Label(card, "TitleText", "強化ショップ", 54, Cream, .06f, .915f, .94f, .975f, TextAlignmentOptions.Left);
        Label(card, "SubText", "ゴールドやジェムでリリィを永続強化", 26, Muted, .06f, .885f, .94f, .92f, TextAlignmentOptions.Left);
        Wallet(card, .815f, .87f);

        // 永続強化の一覧（行は ShopUI が強化データの数だけ並べる）
        RectTransform scroll = Rect(card, "UpgradeScroll", .04f, .30f, .96f, .80f);
        ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        RectTransform viewport = Rect(scroll, "Viewport", 0, 0, 1, 1);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear; // スクロールの当たり判定
        RectTransform content = Rect(viewport, "Content", 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1f);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        // 💎で EXP を買う
        RectTransform pack = Rect(card, "ExpPackBox", .05f, .195f, .95f, .285f);
        Surface(pack, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 18f, 1.5f);
        Picture(pack, "Icon", star, .02f, .15f, .12f, .85f, true);
        TMP_Text packDesc = Label(pack, "DescText", "EXPが525手に入ります", 30, Cream, .14f, .1f, .66f, .9f, TextAlignmentOptions.Left);
        Button packButton = ActionButton(pack, "BuyButton", "", .68f, .12f, .97f, .88f, true, out TMP_Text packLabel);
        Object.DestroyImmediate(packLabel.gameObject);
        Picture(packButton.transform, "GemIcon", gem, .18f, .14f, .42f, .86f, true);
        TMP_Text packCost = Label(packButton.transform, "CostText", "1", 36, Ink, .44f, .1f, .92f, .9f, TextAlignmentOptions.Left);

        // 動画で EXP（1日の上限あり）
        RectTransform ad = Rect(card, "AdBox", .05f, .10f, .95f, .19f);
        Surface(ad, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, 0.45f), 18f, 1.5f);
        TMP_Text adDesc = Label(ad, "DescText", "", 22, Muted, .04f, .06f, .66f, .94f, TextAlignmentOptions.Left);
        adDesc.textWrappingMode = TextWrappingModes.Normal;
        Button adButton = ActionButton(ad, "WatchButton", "動画を見る", .68f, .12f, .97f, .88f, false, out TMP_Text adLabel);
        adLabel.rectTransform.anchorMin = new Vector2(.05f, .42f);
        TMP_Text adCount = Label(adButton.transform, "CountText", "0/5", 24, Gold, .05f, .06f, .95f, .44f);

        view.shopToDetailButton = ActionButton(card, "ToDetailButton", "キャラ詳細へ", .05f, .02f, .485f, .085f, false, out _);
        view.shopCloseButton = ActionButton(card, "CloseButton", "とじる", .515f, .02f, .95f, .085f, false, out _);

        Set(shop, "upgradeRowPrefab", rowPrefab.GetComponent<PartyUpgradeRowUI>());
        Set(shop, "upgradeContent", content);
        Set(shop, "expPackDescText", packDesc); Set(shop, "expPackCostText", packCost); Set(shop, "expPackButton", packButton);
        Set(shop, "adDescText", adDesc); Set(shop, "adCountText", adCount); Set(shop, "adButton", adButton);
        host.SetActive(false);
    }

    // ─── 3) シーンへの設置 ───────────────────────────────────────
    private static void InstallIntoScene(Scene scene, CharacterDefinition lily, List<PartyUpgradeDefinition> upgrades, GameObject rootPrefab)
    {
        GrowthService service = InScene<GrowthService>(scene).FirstOrDefault();
        if (service == null)
        {
            var go = new GameObject("GrowthService");
            SceneManager.MoveGameObjectToScene(go, scene);
            service = go.AddComponent<GrowthService>();
        }
        var so = new SerializedObject(service);
        SerializedProperty party = so.FindProperty("party");
        party.arraySize = 1;
        party.GetArrayElementAtIndex(0).objectReferenceValue = lily;
        SerializedProperty ups = so.FindProperty("upgrades");
        ups.arraySize = upgrades.Count;
        for (int i = 0; i < upgrades.Count; i++) ups.GetArrayElementAtIndex(i).objectReferenceValue = upgrades[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(service);

        if (!InScene<GrowthUIView>(scene).Any())
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);
            instance.name = "GrowthUIRoot";
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static IEnumerable<T> InScene<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    // ─── 共通部品（Quest UI と同じ見た目） ─────────────────────────
    private static RectTransform Rect(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static QuestSurfaceGraphic Surface(RectTransform rt, Color top, Color bottom, Color border, float radius, float borderWidth)
    {
        var g = rt.gameObject.AddComponent<QuestSurfaceGraphic>();
        g.topColor = top; g.bottomColor = bottom; g.borderColor = border;
        g.cornerRadius = radius; g.borderWidth = borderWidth;
        g.raycastTarget = false;
        return g;
    }

    private static TMP_Text Label(Transform parent, string name, string value, float size, Color color,
                                  float x0, float y0, float x1, float y1, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var t = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = value; t.fontSize = size; t.color = color; t.alignment = align;
        t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.6f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private static Image Picture(Transform parent, string name, Sprite sprite, float x0, float y0, float x1, float y1, bool preserve)
    {
        var image = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = preserve; image.raycastTarget = false;
        return image;
    }

    private static void Emblem(RectTransform rt, QuestEmblemGraphic.Symbol symbol)
    {
        var e = rt.gameObject.AddComponent<QuestEmblemGraphic>();
        e.symbol = symbol; e.color = Gold; e.lineWidth = 3f; e.raycastTarget = false;
    }

    private static Button ActionButton(Transform parent, string name, string label, float x0, float y0, float x1, float y1,
                                       bool primary, out TMP_Text labelText)
    {
        RectTransform rt = Rect(parent, name, x0, y0, x1, y1);
        QuestSurfaceGraphic face = primary
            ? Surface(rt, Hex("F0D8A0"), Hex("CDA970"), Hex("F9E9BD"), 22f, 2f)
            : Surface(rt, new Color(0.12f, 0.23f, 0.34f, 0.98f), new Color(0.06f, 0.12f, 0.23f, 0.98f), new Color(0.77f, 0.64f, 0.38f), 22f, 2f);
        face.raycastTarget = true;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .96f, .84f);
        colors.pressedColor = new Color(.76f, .82f, .91f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.55f, .58f, .64f, .75f);
        colors.fadeDuration = .1f;
        button.colors = colors;
        Navigation nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        labelText = Label(rt, "Label", label, 36, primary ? Ink : Cream, .05f, .08f, .95f, .92f);
        return button;
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null) throw new InvalidOperationException($"{target.GetType().Name} に {property} がありません");
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color c);
        return c;
    }
}
