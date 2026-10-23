using System;
using System.Linq;
using Idle;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 本編下部の Lily ステータス HUD（Canvas/PnlTop/PnlSafe/StatusWindow 配下）を生成する。
///
/// ・StatusWindow の GameObject と StatusWindowController（顔スプライト設定）は残し、子だけを作り直す。
/// ・Quest Canvas / QuestUIRoot.prefab / QuestProductionUIBuilder には触れない。
/// ・冒険ボタンは QuestHudButton → QuestEntryController.Open() でダイアログを開く。
/// ・1080x1920（CanvasScaler Match=0）基準。高さは旧ステータスバーと同じ 150 に収める。
/// </summary>
public static class LilyStatusHUDBuilder
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string FontPath = "Assets/Font/NotoSansJP-Bold SDF.asset";

    public const float HudHeight = 150f;
    private const float SideMargin = 16f;
    private const float BottomMargin = 12f;
    private const float PortraitSize = 124f;
    private static readonly Vector2 ButtonSize = new Vector2(236f, 110f);
    // 冒険ボタンの左隣に置く「強化」ボタン（強化ショップを開く）
    private static readonly Vector2 UpgradeButtonSize = new Vector2(168f, 110f);
    private const float ButtonGap = 12f;

    // 顔画像は全身絵から頭部を切り出したスプライト。StatusWindowController の既定値（待機顔）と揃える
    private const float PortraitCropWidth = 0.30f;                           // 元テクスチャ幅のうち丸枠に入れる割合
    private static readonly Vector2 PortraitFocus = new Vector2(0.49f, 0.77f); // 丸枠の中心に置く点（元テクスチャ基準）

    private static readonly Color Cream = Hex("F5EDD8");
    private static readonly Color Gold = Hex("DAC18B");
    private static readonly Color BrightGold = Hex("F0D8A0");
    private static readonly Color Muted = Hex("B8CADA");
    private static readonly Color Ink = Hex("172D43");
    private static readonly Color NavyTop = new Color(0.12f, 0.23f, 0.34f, 0.96f);
    private static readonly Color NavyBottom = new Color(0.06f, 0.12f, 0.23f, 0.96f);
    private static readonly Color FrameGold = new Color(0.77f, 0.64f, 0.38f);

    private static TMP_FontAsset font;

    [MenuItem("LilyClicker/本編HUD/Lilyステータスを再構築（Gameシーン）")]
    public static void BuildFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }
        Build(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[LilyStatusHUD] StatusWindow を再構築しました。シーンを保存してください。");
    }

    /// <summary>batchmode 用: Unity -batchmode -projectPath . -executeMethod LilyStatusHUDBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Build(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[LilyStatusHUD] StatusWindow を再構築して Game.unity を保存しました。");
    }

    public static void Build(Scene scene)
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new InvalidOperationException("TMP font is missing: " + FontPath);

        StatusWindowController controller = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<StatusWindowController>(true))
            .FirstOrDefault();
        if (controller == null) throw new InvalidOperationException("StatusWindowController が Game シーンにありません。");

        RectTransform window = (RectTransform)controller.transform;
        Undo.RegisterFullObjectHierarchyUndo(window.gameObject, "Rebuild Lily Status HUD");

        // 旧ステータスバー（茶色の帯と文字）の中身を撤去する
        for (int i = window.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(window.GetChild(i).gameObject);
        foreach (Graphic graphic in window.GetComponents<Graphic>())
            Object.DestroyImmediate(graphic);

        window.anchorMin = new Vector2(0f, 0f);
        window.anchorMax = new Vector2(1f, 0f);
        window.pivot = new Vector2(0.5f, 0f);
        window.anchoredPosition = Vector2.zero;
        window.sizeDelta = new Vector2(0f, HudHeight);

        // 全画面の透明ボタン ClickOn より手前に置かないと冒険ボタンが押せない。
        // パネル自体は raycastTarget=false なので、ボタン以外の場所をタップしたら従来どおり攻撃になる。
        Transform clickOn = window.parent != null ? window.parent.Find("ClickOn") : null;
        if (clickOn != null && window.GetSiblingIndex() < clickOn.GetSiblingIndex())
            window.SetSiblingIndex(clickOn.GetSiblingIndex());

        RectTransform panel = Surface(window, "HudPanel", NavyTop, NavyBottom, FrameGold, 30f, 3f);
        Stretch(panel, SideMargin, BottomMargin, SideMargin, 0f);

        Image face = BuildPortrait(panel, controller);
        TMP_Text nameText, levelText, hpText;
        Slider hpSlider = BuildInfo(panel, out nameText, out levelText, out hpText);
        BuildAdventureButton(panel);
        BuildUpgradeButton(panel);

        SerializedObject so = new SerializedObject(controller);
        SetRef(so, "faceIcon", face);
        SetRef(so, "hpSlider", hpSlider);
        SetRef(so, "hpFillImage", null);
        SetRef(so, "hpText", hpText);
        SetRef(so, "nameText", nameText);
        SetRef(so, "levelText", levelText);
        SetRef(so, "goldText", null);
        SetRef(so, "scoreText", null);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        // LayoutGroup が決める子の配置と TMP のメッシュを保存前に確定させる。
        // 未計算のまま保存すると、次に Editor で開いた時に再計算されてシーンが変更扱い（Game*）になる。
        foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(true))
            text.ForceMeshUpdate(true, true);
        foreach (LayoutGroup group in window.GetComponentsInChildren<LayoutGroup>(true).Reverse())
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
        Canvas.ForceUpdateCanvases();
    }

    // ─── ポートレート ───────────────────────────────────────────
    private static Image BuildPortrait(RectTransform panel, StatusWindowController controller)
    {
        RectTransform frame = Surface(panel, "PortraitFrame", NavyBottom, NavyBottom, BrightGold, 999f, 5f);
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        frame.anchoredPosition = new Vector2(16f, 0f);

        Color backdrop = new Color(0.20f, 0.32f, 0.46f, 1f);
        RectTransform mask = Surface(frame, "PortraitMask", backdrop, NavyTop, Color.clear, 999f, 0f);
        Stretch(mask, 7f, 7f, 7f, 7f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        // 大きさと位置は StatusWindowController が表情ごとに元テクスチャ基準で合わせる（ShowFace）。
        // ここでは Edit 中の見た目用に、同じ計算で待機顔を置いておく。
        Sprite sprite = FaceSprite(controller);
        RectTransform faceRect = Rect(mask, "FaceIcon");
        faceRect.anchorMin = faceRect.anchorMax = new Vector2(0.5f, 0.5f);
        faceRect.pivot = new Vector2(0.5f, 0.5f);
        if (sprite != null && sprite.texture != null)
        {
            float unitsPerPixel = (PortraitSize - 14f) / (PortraitCropWidth * sprite.texture.width);
            Vector2 focusPixel = new Vector2(PortraitFocus.x * sprite.texture.width, PortraitFocus.y * sprite.texture.height);
            faceRect.sizeDelta = sprite.rect.size * unitsPerPixel;
            faceRect.anchoredPosition = (sprite.rect.center - focusPixel) * unitsPerPixel;
        }
        Image face = faceRect.gameObject.AddComponent<Image>();
        face.sprite = sprite;
        face.preserveAspect = true;
        face.raycastTarget = false;

        // 顔アイコンをタップするとキャラ詳細（レベル・スキル強化）を開く
        QuestSurfaceGraphic frameFace = frame.GetComponent<QuestSurfaceGraphic>();
        frameFace.raycastTarget = true;
        Button button = frame.gameObject.AddComponent<Button>();
        StyleButton(button, frameFace);
        RectTransform badge = Badge(frame, new Vector2(-10f, -10f), 38f);
        LilyEnglish.Growth.UI.GrowthEntryButton entry = frame.gameObject.AddComponent<LilyEnglish.Growth.UI.GrowthEntryButton>();
        entry.target = LilyEnglish.Growth.UI.GrowthEntryButton.Target.CharacterDetail;
        entry.affordableBadge = badge.gameObject;
        return face;
    }

    // ─── 強化ボタン ─────────────────────────────────────────────
    private static void BuildUpgradeButton(RectTransform panel)
    {
        RectTransform rect = Surface(panel, "UpgradeButton", NavyTop, NavyBottom, FrameGold, 26f, 3f);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = UpgradeButtonSize;
        rect.anchoredPosition = new Vector2(-16f - ButtonSize.x - ButtonGap, 0f);
        QuestSurfaceGraphic face = rect.GetComponent<QuestSurfaceGraphic>();
        face.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        StyleButton(button, face);

        RectTransform iconRect = Rect(rect, "StarIcon");
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(46f, 46f);
        iconRect.anchoredPosition = new Vector2(0f, -10f);
        QuestEmblemGraphic icon = iconRect.gameObject.AddComponent<QuestEmblemGraphic>();
        icon.symbol = QuestEmblemGraphic.Symbol.Star;
        icon.lineWidth = 3f;
        icon.color = Gold;
        icon.raycastTarget = false;

        TMP_Text label = Label(rect, "Label", "強化", 38f, Cream, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(-12f, 50f);
        labelRect.anchoredPosition = new Vector2(0f, 8f);

        RectTransform badge = Badge(rect, new Vector2(-8f, -8f), 40f);
        LilyEnglish.Growth.UI.GrowthEntryButton entry = rect.gameObject.AddComponent<LilyEnglish.Growth.UI.GrowthEntryButton>();
        entry.target = LilyEnglish.Growth.UI.GrowthEntryButton.Target.Shop;
        entry.affordableBadge = badge.gameObject;
    }

    /// <summary>右上に少しはみ出す「!」バッジ（強化できるものがある時に出す）</summary>
    private static RectTransform Badge(RectTransform parent, Vector2 offset, float size)
    {
        RectTransform badge = Surface(parent, "AffordableBadge", Hex("D0503F"), Hex("8E2A22"), Hex("F9E9BD"), 999f, 3f);
        badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(size, size);
        badge.anchoredPosition = offset;
        TMP_Text mark = Label(badge, "Mark", "!", size * 0.72f, Cream, TextAlignmentOptions.Center);
        Stretch(mark.rectTransform, 0f, 0f, 0f, 2f);
        badge.gameObject.SetActive(false);
        return badge;
    }

    private static void StyleButton(Button button, Graphic target)
    {
        button.targetGraphic = target;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.84f);
        colors.pressedColor = new Color(0.76f, 0.82f, 0.91f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private static Sprite FaceSprite(StatusWindowController controller)
    {
        SerializedProperty p = new SerializedObject(controller).FindProperty("faceIdle");
        Sprite sprite = p != null ? p.objectReferenceValue as Sprite : null;
        if (sprite != null) return sprite;
        return AssetDatabase.LoadAllAssetsAtPath("Assets/Image/Lily/Face/Lily_Face_Idle.png").OfType<Sprite>().FirstOrDefault();
    }

    // ─── 名前・レベル・HP ───────────────────────────────────────
    private static Slider BuildInfo(RectTransform panel, out TMP_Text nameText, out TMP_Text levelText, out TMP_Text hpText)
    {
        float left = 16f + PortraitSize + 22f;
        float right = 16f + ButtonSize.x + ButtonGap + UpgradeButtonSize.x + 26f;

        RectTransform info = Rect(panel, "Info");
        Stretch(info, left, 20f, right, 18f);

        RectTransform nameRow = Rect(info, "NameRow");
        nameRow.anchorMin = new Vector2(0f, 1f);
        nameRow.anchorMax = new Vector2(1f, 1f);
        nameRow.pivot = new Vector2(0f, 1f);
        nameRow.sizeDelta = new Vector2(0f, 54f);
        nameRow.anchoredPosition = Vector2.zero;
        HorizontalLayoutGroup row = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.LowerLeft;
        row.spacing = 16f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        nameText = Label(nameRow, "NameText", "LILY", 42f, Cream, TextAlignmentOptions.BottomLeft);
        nameText.characterSpacing = 8f;
        levelText = Label(nameRow, "LevelText", "Lv.1", 32f, Gold, TextAlignmentOptions.BottomLeft);

        hpText = Label(info, "HpText", "100 / 100", 26f, Muted, TextAlignmentOptions.BottomRight);
        RectTransform hpRect = hpText.rectTransform;
        hpRect.anchorMin = new Vector2(0.5f, 1f);
        hpRect.anchorMax = new Vector2(1f, 1f);
        hpRect.pivot = new Vector2(1f, 1f);
        hpRect.sizeDelta = new Vector2(0f, 50f);
        hpRect.anchoredPosition = Vector2.zero;

        TMP_Text hpLabel = Label(info, "HpLabel", "HP", 26f, Gold, TextAlignmentOptions.MidlineLeft);
        RectTransform hpLabelRect = hpLabel.rectTransform;
        hpLabelRect.anchorMin = new Vector2(0f, 0f);
        hpLabelRect.anchorMax = new Vector2(0f, 0f);
        hpLabelRect.pivot = new Vector2(0f, 0f);
        hpLabelRect.sizeDelta = new Vector2(48f, 30f);
        hpLabelRect.anchoredPosition = Vector2.zero;

        RectTransform gauge = Surface(info, "HpGauge", Ink, Ink, new Color(0.77f, 0.64f, 0.38f, 0.85f), 15f, 2f);
        gauge.anchorMin = new Vector2(0f, 0f);
        gauge.anchorMax = new Vector2(1f, 0f);
        gauge.pivot = new Vector2(0f, 0f);
        gauge.offsetMin = new Vector2(52f, 1f);
        gauge.offsetMax = new Vector2(0f, 29f);

        RectTransform fillArea = Rect(gauge, "FillArea");
        Stretch(fillArea, 4f, 4f, 4f, 4f);
        RectTransform fill = Surface(fillArea, "Fill", Hex("B2DDD2"), Hex("5F9C9A"), Color.clear, 11f, 0f);
        Stretch(fill, 0f, 0f, 0f, 0f);

        Slider slider = gauge.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        return slider;
    }

    // ─── 冒険ボタン ─────────────────────────────────────────────
    private static void BuildAdventureButton(RectTransform panel)
    {
        RectTransform rect = Surface(panel, "AdventureButton", NavyTop, NavyBottom, FrameGold, 26f, 3f);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = ButtonSize;
        rect.anchoredPosition = new Vector2(-16f, 0f);
        QuestSurfaceGraphic face = rect.GetComponent<QuestSurfaceGraphic>();
        face.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.84f);
        colors.pressedColor = new Color(0.76f, 0.82f, 0.91f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        RectTransform stack = Rect(rect, "Stack");
        Stretch(stack, 8f, 8f, 8f, 8f);
        VerticalLayoutGroup column = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        column.childAlignment = TextAnchor.MiddleCenter;
        column.spacing = 2f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;

        RectTransform titleRow = Rect(stack, "TitleRow");
        HorizontalLayoutGroup row = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 8f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        RectTransform iconRect = Rect(titleRow, "SwordsIcon");
        QuestEmblemGraphic icon = iconRect.gameObject.AddComponent<QuestEmblemGraphic>();
        icon.symbol = QuestEmblemGraphic.Symbol.Swords;
        icon.lineWidth = 3f;
        icon.color = Gold;
        icon.raycastTarget = false;
        LayoutElement iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 50f;
        iconLayout.preferredHeight = 50f;

        TMP_Text label = Label(titleRow, "Label", "冒険", 42f, Cream, TextAlignmentOptions.Center);

        RectTransform pill = Surface(stack, "ProgressPill", new Color(0.03f, 0.07f, 0.13f, 0.85f),
                                     new Color(0.03f, 0.07f, 0.13f, 0.85f), new Color(0.77f, 0.64f, 0.38f, 0.6f), 15f, 1.5f);
        LayoutElement pillLayout = pill.gameObject.AddComponent<LayoutElement>();
        pillLayout.preferredWidth = 118f;
        pillLayout.preferredHeight = 32f;
        TMP_Text progress = Label(pill, "ProgressText", "0 / 10", 24f, Gold, TextAlignmentOptions.Center);
        Stretch(progress.rectTransform, 0f, 0f, 0f, 0f);

        // 解放後に出す「!」バッジ（ボタンの右上に少しはみ出して置く。レイアウト対象外）
        RectTransform badge = Surface(rect, "UnlockedBadge", Hex("D0503F"), Hex("8E2A22"), Hex("F9E9BD"), 999f, 3f);
        badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(44f, 44f);
        badge.anchoredPosition = new Vector2(-8f, -8f);
        TMP_Text mark = Label(badge, "Mark", "!", 32f, Cream, TextAlignmentOptions.Center);
        Stretch(mark.rectTransform, 0f, 0f, 0f, 2f);
        badge.gameObject.SetActive(false);

        QuestHudButton hud = rect.gameObject.AddComponent<QuestHudButton>();
        hud.face = face;
        hud.label = label;
        hud.icon = icon;
        hud.progressRoot = pill.gameObject;
        hud.progressText = progress;
        hud.unlockedBadge = badge;
    }

    // ─── 共通部品 ───────────────────────────────────────────────
    private static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static RectTransform Surface(Transform parent, string name, Color top, Color bottom, Color border, float radius, float borderWidth)
    {
        RectTransform rt = Rect(parent, name);
        QuestSurfaceGraphic graphic = rt.gameObject.AddComponent<QuestSurfaceGraphic>();
        graphic.topColor = top;
        graphic.bottomColor = bottom;
        graphic.borderColor = border;
        graphic.cornerRadius = radius;
        graphic.borderWidth = borderWidth;
        graphic.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p == null) throw new InvalidOperationException("StatusWindowController missing field " + property);
        p.objectReferenceValue = value;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color c);
        return c;
    }
}
