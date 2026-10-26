using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Idle;
using LilyEnglish.Growth.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 本編のステージ制（拠点・ステージ選択・クリア画面・戦闘中の表示）を Game シーンに組み立てる。
/// 1) ステージカード（StageCard）と、拠点・クリア画面を持つ StageUIRoot のプレハブを Assets/Assets/Prefabs/Stage に作る
/// 2) 本編 Canvas の PnlSafe 下に StageUI（戦闘中のステージ名・退却ボタン・開始バナー）を作り直し、
///    StageDirector に本編背景・GameManager・両画面を結び付ける
/// 見た目は Quest UI と同じ紺・金・クリーム。位置はアンカー基準（固定座標で画面端に寄せない）。
/// プレハブやシーンを手で直さず、このビルダーを直して再実行する。
/// </summary>
public static class StageUIBuilder
{
    public const string ScenePath = "Assets/Scenes/Game.unity";
    public const string RootName = "StageUI";
    public const string PrefabDir = "Assets/Assets/Prefabs/Stage";
    public const string CardPrefabPath = PrefabDir + "/StageCard.prefab";
    public const string RootPrefabPath = PrefabDir + "/StageUIRoot.prefab";
    private const string CoinSpritePath = "Assets/Image/UI/Coin.png";
    private const string BackdropFallback = "Assets/Resources/Backgrounds/stage_01.jpg";

    // 拠点・クリア画面の Canvas。強化画面（105）より下、クエスト（100）より上
    private const int OverlaySortingOrder = 102;
    private const float CardHeight = 400f;
    private static readonly Vector2 ResultCardSize = new Vector2(900f, 1180f);

    private static readonly Color Cream = Hex("F5EDD8");
    private static readonly Color Gold = Hex("DAC18B");
    private static readonly Color Muted = Hex("B8CADA");
    private static readonly Color Ink = Hex("172D43");
    private static readonly Color Coral = Hex("D0503F");
    private static readonly Color Inset = new Color(0.03f, 0.07f, 0.13f, 0.78f);
    private static TMP_FontAsset font;
    private static Sprite coin;

    [MenuItem("LilyClicker/ステージ/拠点・ステージ画面を再構築（Gameシーン）")]
    public static void BuildFromMenu()
    {
        string error = BuildAll();
        if (error != null) EditorUtility.DisplayDialog("ステージ画面の再構築", error, "OK");
    }

    /// <summary>batchmode 用: Unity -batchmode -executeMethod StageUIBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        string error = BuildAll();
        if (error != null) Debug.LogError(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    [MenuItem("LilyClicker/ステージ/進行をリセット（検証用）")]
    public static void ResetProgress()
    {
        StageProgress.ResetAll();
        Debug.Log("[StageUIBuilder] ステージの解放・クリア状況をリセットしました（ゴールド・EXP・強化は変えていません）。");
    }

    [MenuItem("LilyClicker/ステージ/全ステージを解放（検証用）")]
    public static void UnlockAll()
    {
        StageProgress.UnlockAllForDebug();
        Debug.Log("[StageUIBuilder] 全ステージを解放しました。");
    }

    /// <summary>成功なら null、失敗・中止ならその理由を返す</summary>
    public static string BuildAll()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded && scene.isDirty)
            return "Game シーンに未保存の変更があるため中止しました。保存してから再実行してください。";

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(QuestTMPStabilizer.QuestFontPath);
        if (font == null) return "TMP フォントが見つかりません: " + QuestTMPStabilizer.QuestFontPath;
        coin = SpriteAt(CoinSpritePath);
        Directory.CreateDirectory(PrefabDir);

        GameObject cardPrefab = BuildCardPrefab();
        GameObject rootPrefab = BuildRootPrefab(cardPrefab);

        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        string error = InstallIntoScene(scene, rootPrefab);
        if (error == null) EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        if (error == null) Debug.Log("[StageUIBuilder] 拠点・ステージ画面を再構築しました。");
        return error;
    }

    // ─── 1) ステージカード ────────────────────────────────────────
    private static GameObject BuildCardPrefab()
    {
        RectTransform root = Rect(null, "StageCard", 0, 0, 1, 1);
        Surface(root, Rgba(.12f, .23f, .34f, .97f), Rgba(.06f, .12f, .23f, .97f), Gold, 28f, 3f);
        var layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = CardHeight;
        layout.minHeight = CardHeight;
        layout.flexibleWidth = 1f;
        var card = root.gameObject.AddComponent<StageCardUI>();

        // 左：背景のサムネイル（縦 2:3 の絵がそのまま入る比率）
        RectTransform frame = Box(root, "ThumbFrame", new Vector2(0, 0), new Vector2(0, 1), new Vector2(16, 16), new Vector2(268, -16));
        Surface(frame, Hex("0B1626"), Hex("0B1626"), Gold, 20f, 3f);
        RectTransform art = Box(frame, "Thumbnail", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        Image thumb = art.gameObject.AddComponent<Image>();
        thumb.preserveAspect = true;
        thumb.raycastTarget = false;
        RectTransform lockOverlay = Box(frame, "LockOverlay", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        Image shade = lockOverlay.gameObject.AddComponent<Image>();
        shade.color = new Color(.03f, .06f, .12f, .78f);
        shade.raycastTarget = false;
        RectTransform key = Rect(lockOverlay, "LockIcon", .5f, .5f, .5f, .5f);
        key.sizeDelta = new Vector2(110, 110);
        Emblem(key, QuestEmblemGraphic.Symbol.Lock, 5f);

        // 右：番号・名前・推奨レベル・ボタン
        RectTransform info = Box(root, "Info", Vector2.zero, Vector2.one, new Vector2(292, 16), new Vector2(-22, -16));
        TMP_Text number = Label(info, "NumberText", "STAGE 1", 40, Gold, 0f, .76f, .58f, 1f, TextAlignmentOptions.Left);
        TMP_Text stageName = Label(info, "NameText", "ひだまりの森", 58, Cream, 0f, .47f, 1f, .76f, TextAlignmentOptions.Left);
        TMP_Text recommend = Label(info, "RecommendText", "推奨 Lv.1", 34, Cream, 0f, .27f, 1f, .47f, TextAlignmentOptions.Left);

        RectTransform clear = Rect(info, "ClearBadge", .60f, .78f, 1f, .99f);
        Surface(clear, Hex("F0D8A0"), Hex("CDA970"), Hex("F9E9BD"), 999f, 2f);
        Label(clear, "Label", "CLEAR", 34, Ink, .08f, .08f, .92f, .92f);
        RectTransform fresh = Rect(info, "NewBadge", .60f, .78f, 1f, .99f);
        Surface(fresh, Hex("E8705C"), Hex("C0453A"), Hex("F9E9BD"), 999f, 2f);
        Label(fresh, "Label", "NEW!", 34, Cream, .08f, .08f, .92f, .92f);

        Button challenge = ActionButton(info, "ChallengeButton", "挑戦", .44f, 0f, 1f, .27f, true, out TMP_Text challengeLabel);
        TMP_Text locked = Label(info, "LockedText", "STAGE 1 をクリアで解放", 34, Muted, 0f, 0f, 1f, .27f, TextAlignmentOptions.Left);

        Set(card, "thumbnail", thumb);
        Set(card, "lockOverlay", lockOverlay.gameObject);
        Set(card, "numberText", number);
        Set(card, "nameText", stageName);
        Set(card, "recommendText", recommend);
        Set(card, "clearBadge", clear.gameObject);
        Set(card, "newBadge", fresh.gameObject);
        Set(card, "lockedText", locked);
        Set(card, "challengeButton", challenge);
        Set(card, "challengeLabel", challengeLabel);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardPrefabPath);
        Object.DestroyImmediate(root.gameObject);
        return prefab;
    }

    // ─── 2) 拠点とクリア画面 ──────────────────────────────────────
    private static GameObject BuildRootPrefab(GameObject cardPrefab)
    {
        RectTransform root = Rect(null, "StageUIRoot", 0, 0, 1, 1);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        root.gameObject.AddComponent<GraphicRaycaster>();

        BuildSelectPanel(root, cardPrefab.GetComponent<StageCardUI>());
        BuildResultPanel(root);
        BuildDefeatPanel(root);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RootPrefabPath);
        Object.DestroyImmediate(root.gameObject);
        return prefab;
    }

    private static void BuildSelectPanel(RectTransform root, StageCardUI cardPrefab)
    {
        RectTransform panel = Rect(root, "StageSelectPanel", 0, 0, 1, 1);
        var view = panel.gameObject.AddComponent<StageSelectView>();

        // 背景は全画面を覆う（縦長の絵を、画面の縦横比に関わらず隙間なく敷く）
        RectTransform backdropRect = Rect(panel, "Backdrop", .5f, .5f, .5f, .5f);
        backdropRect.sizeDelta = new Vector2(1080f, 1620f);
        var fitter = backdropRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1696f / 2528f;
        Image backdrop = backdropRect.gameObject.AddComponent<Image>();
        backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropFallback);
        backdrop.raycastTarget = false;
        // 文字が読めるよう暗くする。同時に、背後の本編（タップ攻撃）への入力を遮る
        Image dim = Picture(panel, "Dim", null, 0, 0, 1, 1);
        dim.color = new Color(.03f, .06f, .12f, .66f);
        dim.raycastTarget = true;

        RectTransform safe = Rect(panel, "SafeArea", 0, 0, 1, 1);
        safe.gameObject.AddComponent<QuestSafeArea>();

        // 上：タイトル・リリィのレベル・ゴールドとEXP
        RectTransform header = Box(safe, "Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -300), new Vector2(-40, -24));
        Label(header, "Title", "拠点", 84, Gold, 0f, .5f, .5f, 1f, TextAlignmentOptions.Left);
        Label(header, "Subtitle", "冒険の行き先を選ぼう", 34, Cream, 0f, .44f, .6f, .62f, TextAlignmentOptions.Left);
        TMP_Text level = Label(header, "LevelText", "リリィ Lv.1", 46, Cream, .5f, .74f, 1f, .98f, TextAlignmentOptions.Right);
        // 右上には別機能の「今日のボーナス」ボタン（画面の上から8〜13%、右36%）が手前に出るので、ウォレット欄は左側に収める
        RectTransform wallet = Rect(header, "WalletBar", 0f, 0f, .60f, .40f);
        Surface(wallet, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, .45f), 18f, 1.5f);
        TMP_Text gold = WalletGroup(wallet, "Gold", coin, null, 0f, .5f);
        TMP_Text exp = WalletGroup(wallet, "Exp", null, "EXP", .5f, 1f);

        // 中：ステージの一覧（縦スクロール）
        RectTransform scrollRect = Box(safe, "StageScroll", Vector2.zero, Vector2.one, new Vector2(40, 214), new Vector2(-40, -312));
        Image catcher = scrollRect.gameObject.AddComponent<Image>();
        catcher.color = new Color(0, 0, 0, 0); // ドラッグを拾うための透明な面
        RectTransform viewport = Rect(scrollRect, "Viewport", 0, 0, 1, 1);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect(viewport, "Content", 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1f);
        var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 26f;
        list.padding = new RectOffset(0, 0, 8, 24);
        list.childAlignment = TextAnchor.UpperCenter;
        list.childControlWidth = true;
        list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        var sizer = content.gameObject.AddComponent<ContentSizeFitter>();
        sizer.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 60f;
        scroll.decelerationRate = .135f;

        // 下：強化へ（既存の強化画面を開く。強化できるものがあれば「!」）
        RectTransform bottom = Box(safe, "BottomBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 40), new Vector2(-40, 180));
        Button shop = ActionButton(bottom, "ShopButton", "強化ショップ", 0f, 0f, .485f, 1f, true, out _);
        Button hero = ActionButton(bottom, "CharacterButton", "リリィを育てる", .515f, 0f, 1f, 1f, false, out _);
        AttachEntry(shop, GrowthEntryButton.Target.Shop);
        AttachEntry(hero, GrowthEntryButton.Target.CharacterDetail);

        Set(view, "backdrop", backdrop);
        Set(view, "scroll", scroll);
        Set(view, "content", content);
        Set(view, "cardPrefab", cardPrefab);
        Set(view, "goldText", gold);
        Set(view, "expText", exp);
        Set(view, "levelText", level);
        panel.gameObject.SetActive(false);
    }

    private static void BuildResultPanel(RectTransform root)
    {
        RectTransform panel = Rect(root, "StageResultPanel", 0, 0, 1, 1);
        var view = panel.gameObject.AddComponent<StageResultView>();
        Image dim = Picture(panel, "DimOverlay", null, 0, 0, 1, 1);
        dim.color = new Color(.02f, .04f, .08f, .78f);
        dim.raycastTarget = true;

        RectTransform safe = Rect(panel, "SafeArea", 0, 0, 1, 1);
        safe.gameObject.AddComponent<QuestSafeArea>();
        RectTransform area = Rect(safe, "CardArea", 0, 0, 1, 1);
        area.offsetMin = new Vector2(60f, 60f);
        area.offsetMax = new Vector2(-60f, -60f);
        RectTransform card = Rect(area, "Card", .5f, .5f, .5f, .5f);
        card.sizeDelta = ResultCardSize;
        var fitter = card.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = ResultCardSize.x / ResultCardSize.y;
        Surface(card, Rgba(.12f, .23f, .34f, .98f), Rgba(.06f, .12f, .23f, .98f), Gold, 36f, 4f).raycastTarget = true;

        RectTransform starL = Rect(card, "StarLeft", .05f, .835f, .17f, .93f);
        Emblem(starL, QuestEmblemGraphic.Symbol.Star, 4f);
        RectTransform starR = Rect(card, "StarRight", .83f, .835f, .95f, .93f);
        Emblem(starR, QuestEmblemGraphic.Symbol.Star, 4f);
        TMP_Text title = Label(card, "TitleText", "STAGE CLEAR!", 92, Gold, .12f, .81f, .88f, .955f);
        TMP_Text stageName = Label(card, "StageNameText", "STAGE 1　ひだまりの森", 48, Cream, .06f, .72f, .94f, .81f);

        RectTransform rewards = Rect(card, "RewardPanel", .07f, .43f, .93f, .69f);
        Surface(rewards, Inset, Inset, new Color(Gold.r, Gold.g, Gold.b, .45f), 22f, 2f);
        Label(rewards, "GoldLabel", "獲得 GOLD", 40, Muted, .05f, .52f, .5f, .92f, TextAlignmentOptions.Left);
        TMP_Text goldValue = Label(rewards, "GoldValue", "0 G", 62, Gold, .42f, .52f, .96f, .92f, TextAlignmentOptions.Right);
        Label(rewards, "ExpLabel", "獲得 EXP", 40, Muted, .05f, .08f, .5f, .48f, TextAlignmentOptions.Left);
        TMP_Text expValue = Label(rewards, "ExpValue", "0 EXP", 62, Gold, .42f, .08f, .96f, .48f, TextAlignmentOptions.Right);
        TMP_Text expNote = Label(card, "ExpNoteText", "クリアボーナス +0 を含む", 30, Muted, .07f, .365f, .93f, .43f);
        TMP_Text unlock = Label(card, "UnlockText", "STAGE 2「花咲く丘」が解放された！", 38, Hex("F2C879"), .05f, .28f, .95f, .365f);

        Button toBase = ActionButton(card, "BaseButton", "拠点に戻る", .08f, .125f, .92f, .245f, true, out _);
        Button retry = ActionButton(card, "RetryButton", "もう一度挑戦", .08f, .03f, .92f, .11f, false, out _);

        Set(view, "titleText", title);
        Set(view, "stageNameText", stageName);
        Set(view, "goldValueText", goldValue);
        Set(view, "expValueText", expValue);
        Set(view, "expNoteText", expNote);
        Set(view, "unlockText", unlock);
        Set(view, "baseButton", toBase);
        Set(view, "retryButton", retry);
        panel.gameObject.SetActive(false);
    }

    private static void BuildDefeatPanel(RectTransform root)
    {
        RectTransform panel = Rect(root, "StageDefeatPanel", 0, 0, 1, 1);
        var view = panel.gameObject.AddComponent<StageDefeatView>();
        var defeatCanvas = panel.gameObject.AddComponent<Canvas>();
        defeatCanvas.overrideSorting = true;
        defeatCanvas.sortingOrder = 200;
        panel.gameObject.AddComponent<GraphicRaycaster>();
        Image dim = Picture(panel, "DimOverlay", null, 0, 0, 1, 1);
        dim.color = new Color(.02f, .04f, .08f, .84f);
        dim.raycastTarget = true;
        RectTransform safe = Rect(panel, "SafeArea", 0, 0, 1, 1);
        safe.gameObject.AddComponent<QuestSafeArea>();
        RectTransform area = Rect(safe, "CardArea", 0, 0, 1, 1);
        area.offsetMin = new Vector2(60, 60); area.offsetMax = new Vector2(-60, -60);
        RectTransform card = Rect(area, "Card", .5f, .5f, .5f, .5f);
        card.sizeDelta = ResultCardSize;
        var fit = card.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = ResultCardSize.x / ResultCardSize.y;
        Surface(card, Rgba(.12f, .23f, .34f, .98f), Rgba(.06f, .12f, .23f, .98f), Gold, 36, 4).raycastTarget = true;
        Label(card, "TitleText", "GAME OVER", 84, Gold, .07f, .85f, .93f, .96f);
        Label(card, "Message", "リリィは倒れてしまった…", 46, Cream, .05f, .76f, .95f, .85f);
        Picture(card, "Lily", SpriteAt("Assets/Image/Lily/Lily_Damage.png"), .24f, .44f, .76f, .76f, true);
        TMP_Text note = Label(card, "StageText", "", 32, Muted, .06f, .285f, .94f, .44f);
        Button retry = ActionButton(card, "RetryButton", "もう一度挑戦", .08f, .15f, .92f, .265f, true, out _);
        Button toBase = ActionButton(card, "BaseButton", "拠点に戻る", .08f, .035f, .92f, .13f, false, out _);
        Set(view, "stageText", note); Set(view, "retryButton", retry); Set(view, "baseButton", toBase);
        panel.gameObject.SetActive(false);
    }

    private static TMP_Text WalletGroup(RectTransform bar, string key, Sprite icon, string label, float x0, float x1)
    {
        RectTransform group = Rect(bar, key, x0, 0, x1, 1);
        if (icon != null) Picture(group, "Icon", icon, .06f, .2f, .24f, .8f, true);
        else Label(group, "Icon", label, 32, Gold, .03f, .1f, .27f, .9f);
        return Label(group, "Value", "0", 46, Cream, .27f, .1f, .96f, .9f, TextAlignmentOptions.Left);
    }

    private static void AttachEntry(Button button, GrowthEntryButton.Target target)
    {
        RectTransform rect = (RectTransform)button.transform;
        RectTransform badge = Rect(rect, "AffordableBadge", 1f, 1f, 1f, 1f);
        Surface(badge, Hex("D0503F"), Hex("8E2A22"), Hex("F9E9BD"), 999f, 3f);
        badge.pivot = new Vector2(.5f, .5f);
        badge.sizeDelta = new Vector2(48f, 48f);
        badge.anchoredPosition = new Vector2(-10f, -10f);
        TMP_Text mark = Label(badge, "Mark", "!", 36, Cream, 0f, 0f, 1f, 1f);
        mark.rectTransform.offsetMax = new Vector2(0f, 2f);
        badge.gameObject.SetActive(false);
        var entry = button.gameObject.AddComponent<GrowthEntryButton>();
        entry.target = target;
        entry.affordableBadge = badge.gameObject;
    }

    // ─── 3) シーンへの配置 ────────────────────────────────────────
    private static string InstallIntoScene(Scene scene, GameObject rootPrefab)
    {
        Image background = InScene<BackgroundScroller>(scene).Select(s => s.GetComponent<Image>()).FirstOrDefault(i => i != null);
        if (background == null) return "本編の背景（BackgroundScroller の付いた Image）が見つかりません。";
        RectTransform safe = InScene<RectTransform>(scene).FirstOrDefault(t => t.name == "PnlSafe" && t.Find("Text_Score") != null);
        if (safe == null) return "本編 Canvas の PnlSafe（Text_Score を含む）が見つかりません。";
        Idle.GameManager gameManager = InScene<Idle.GameManager>(scene).FirstOrDefault();
        if (gameManager == null) return "GameManager が見つかりません。";

        foreach (GameObject root in scene.GetRootGameObjects().Where(r => r.name == "StageUIRoot").ToList()) Object.DestroyImmediate(root);
        Transform old = safe.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var overlay = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);
        overlay.name = "StageUIRoot";
        StageSelectView select = overlay.GetComponentInChildren<StageSelectView>(true);
        StageResultView result = overlay.GetComponentInChildren<StageResultView>(true);

        // 戦闘中の表示（本編 Canvas の SafeArea 内）
        RectTransform stageUi = Rect(safe, RootName, 0f, 0f, 1f, 1f);
        stageUi.SetAsLastSibling();
        var director = stageUi.gameObject.AddComponent<StageDirector>();

        RectTransform hud = Rect(stageUi, "BattleHud", 0f, 0f, 1f, 1f);
        // 上部バー（GOLD/SCORE）の下の左側に置く。右側には別機能の「今日のボーナス」ボタンが出る
        RectTransform plate = Rect(hud, "StagePlate", 0f, 1f, 0f, 1f);
        plate.pivot = new Vector2(0f, 1f);
        plate.sizeDelta = new Vector2(470f, 104f);
        plate.anchoredPosition = new Vector2(206f, -146f);
        Surface(plate, Rgba(.10f, .20f, .32f, .90f), Rgba(.05f, .11f, .21f, .90f), Gold, 30f, 2f);
        TMP_Text plateName = Label(plate, "StageNameText", "STAGE 1　ひだまりの森", 34, Cream, .05f, .5f, .95f, .94f);
        TMP_Text plateProgress = Label(plate, "StageProgressText", "撃破 0 / 25", 30, Gold, .05f, .06f, .95f, .5f);

        // 退却（拠点へ）。2回押して戻る。ステージ名の左隣に置く
        RectTransform retreat = Rect(hud, "RetreatButton", 0f, 1f, 0f, 1f);
        retreat.pivot = new Vector2(0f, 1f);
        retreat.sizeDelta = new Vector2(170f, 64f);
        retreat.anchoredPosition = new Vector2(24f, -146f);
        Surface(retreat, Rgba(.12f, .23f, .34f, .96f), Rgba(.06f, .12f, .23f, .96f), Gold, 20f, 2f).raycastTarget = true;
        Button retreatButton = retreat.gameObject.AddComponent<Button>();
        retreatButton.targetGraphic = retreat.GetComponent<Graphic>();
        Navigation nav = retreatButton.navigation; nav.mode = Navigation.Mode.None; retreatButton.navigation = nav;
        TMP_Text retreatLabel = Label(retreat, "Label", "拠点へ", 28, Cream, .04f, .06f, .96f, .94f);
        hud.gameObject.SetActive(false);

        // 開始・ボス出現のバナー（画面中央より少し上。タップは素通し）
        RectTransform banner = Rect(stageUi, "StageBanner", 0f, .6f, 1f, .6f);
        banner.sizeDelta = new Vector2(-96f, 230f);
        banner.anchoredPosition = Vector2.zero;
        Surface(banner, Rgba(.10f, .20f, .32f, .92f), Rgba(.04f, .09f, .18f, .92f), Gold, 36f, 3f);
        CanvasGroup group = banner.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        TMP_Text title = Label(banner, "BannerTitleText", "STAGE 1", 88, Gold, .04f, .42f, .96f, .94f);
        TMP_Text sub = Label(banner, "BannerSubText", "ひだまりの森", 50, Cream, .04f, .08f, .96f, .44f);
        banner.gameObject.SetActive(false);

        Set(director, "gameManager", gameManager);
        Set(director, "background", background);
        Set(director, "battleHud", hud.gameObject);
        Set(director, "plateNameText", plateName);
        Set(director, "plateProgressText", plateProgress);
        Set(director, "retreatButton", retreatButton);
        Set(director, "retreatLabel", retreatLabel);
        Set(director, "banner", group);
        Set(director, "bannerTitleText", title);
        Set(director, "bannerSubText", sub);
        Set(director, "selectView", select);
        Set(director, "resultView", result);
        Set(director, "defeatView", overlay.GetComponentInChildren<StageDefeatView>(true));
        Set(gameManager, "stageDirector", director);

        EditorSceneManager.MarkSceneDirty(scene);
        return null;
    }

    // ─── 共通部品（Quest UI と同じ見た目） ─────────────────────────
    private static IEnumerable<T> InScene<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

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

    private static RectTransform Box(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = Rect(parent, name, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
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

    private static Image Picture(Transform parent, string name, Sprite sprite, float x0, float y0, float x1, float y1, bool preserve = false)
    {
        var image = Rect(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = preserve; image.raycastTarget = false;
        return image;
    }

    private static void Emblem(RectTransform rt, QuestEmblemGraphic.Symbol symbol, float lineWidth)
    {
        var e = rt.gameObject.AddComponent<QuestEmblemGraphic>();
        e.symbol = symbol; e.color = Gold; e.lineWidth = lineWidth; e.raycastTarget = false;
    }

    private static Button ActionButton(Transform parent, string name, string label, float x0, float y0, float x1, float y1,
                                       bool primary, out TMP_Text labelText)
    {
        RectTransform rt = Rect(parent, name, x0, y0, x1, y1);
        QuestSurfaceGraphic face = primary
            ? Surface(rt, Hex("F0D8A0"), Hex("CDA970"), Hex("F9E9BD"), 24f, 2f)
            : Surface(rt, Rgba(.12f, .23f, .34f, .98f), Rgba(.06f, .12f, .23f, .98f), Gold, 24f, 2f);
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
        labelText = Label(rt, "Label", label, 44, primary ? Ink : Cream, .05f, .08f, .95f, .92f);
        return button;
    }

    private static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null) throw new InvalidOperationException($"{target.GetType().Name} に {property} がありません");
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    private static Color Rgba(float r, float g, float b, float a) => new Color(r, g, b, a);

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color c);
        return c;
    }
}
