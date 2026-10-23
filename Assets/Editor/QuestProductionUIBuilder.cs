using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>表示Prefabの生成と既存QuestManagerへの配線。ゲームルール・設定は変更しない。</summary>
public static class QuestProductionUIBuilder
{
    public const string PrefabPath = "Assets/Assets/Prefabs/QuestUIRoot.prefab";
    // 旧「開始カード」の入れ子Prefab。現在の QuestUIRoot では使わない（startCard が null なら保存しない）
    public const string StartCardPath = "Assets/Assets/Prefabs/QuestStartCard.prefab";
    private static TMP_FontAsset font;
    private static Sprite forest;
    private static Sprite lily;
    private static readonly Color Cream = Hex("F5EDD8");
    private static readonly Color Gold = Hex("DAC18B");
    private static readonly Color Muted = Hex("B8CADA");
    private static readonly Color Ink = Hex("172D43");

    // 冒険クエストの入口モーダル（1080x1920 基準）。旧「冒険クエスト」大型パネル（918x576）と同じ幅・意匠で中央に表示する。
    // 開くボタンは本編下部の Lily ステータス HUD 側にある「⚔ 冒険」（LilyStatusHUDBuilder が生成）で、
    // QuestEntryController.Open() を呼ぶ。このプレハブ側には常設ボタンを置かない。
    public static readonly Vector2 EntryDialogSize = new Vector2(918f, 760f);

    [MenuItem("LilyClicker/製品版Quest UI/PrefabとGame・Questシーンを更新")]
    public static void BuildAll()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GameObject previousPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Dictionary<string, Sprite> prefabImages = CaptureImages(previousPrefab != null ? previousPrefab.GetComponent<QuestUIView>() : null);
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        QuestUIView view = BuildView();
        RestoreImages(view, prefabImages);
        if (view.startCard != null)
            PrefabUtility.SaveAsPrefabAssetAndConnect(view.startCard, StartCardPath, InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath);
        Object.DestroyImmediate(view.gameObject);
        foreach (string path in new[] { "Assets/Scenes/Game.unity", "Assets/Scenes/Quest.unity" })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            InstallCurrentScene();
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode && previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
        Debug.Log("[QuestProductionUI] Prefabs and both scenes updated. Quest gameplay data preserved.");
    }

    [MenuItem("LilyClicker/製品版Quest UI/現在のシーンに組み込む")]
    private static void InstallFromMenu() { InstallCurrentScene(); }

    public static QuestUIView InstallCurrentScene()
    {
        QuestManager manager = InScene<QuestManager>().FirstOrDefault();
        if (manager == null) manager = new GameObject("QuestManager").AddComponent<QuestManager>();

        // 現在割り当て済みのアートを保存し、UI差し替え後も維持する。
        Dictionary<string, Object> art = CaptureArt(manager);
        QuestUIView oldView = InScene<QuestUIView>().FirstOrDefault();
        Dictionary<string, Sprite> images = CaptureImages(oldView);
        foreach (QuestUIView old in InScene<QuestUIView>())
            Object.DestroyImmediate(old.gameObject);
        foreach (Transform old in InScene<Transform>())
        {
            if (old == null) continue;
            if (old.name == "QuestUIRoot" || old.name == "DEBUG_StartQuestButton") Object.DestroyImmediate(old.gameObject);
        }
        foreach (QuestStartButton old in InScene<QuestStartButton>())
            Object.DestroyImmediate(old.gameObject);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        QuestUIView view = asset != null ? ((GameObject)PrefabUtility.InstantiatePrefab(asset)).GetComponent<QuestUIView>() : BuildView();
        manager.choicePanel = view.choicePanel;
        manager.villagerPanel = view.villagerPanel;
        manager.chestPanel = view.chestPanel;
        manager.battlePanel = view.battlePanel;
        manager.clearPanel = view.clearPanel;
        manager.pipHint = view.pipHint;
        RestoreArt(manager, art);
        RestoreImages(view, images);

        QuestOverlayController overlay = manager.GetComponent<QuestOverlayController>();
        if (overlay == null) overlay = manager.gameObject.AddComponent<QuestOverlayController>();
        overlay.questRoot = view.questRoot;
        overlay.openButton = view.openButton;
        var hide = new List<GameObject>();
        if (view.startCard != null) hide.Add(view.startCard);
        foreach (Canvas canvas in InScene<Canvas>())
            if (canvas != null && canvas.isRootCanvas && canvas.gameObject != view.gameObject && canvas.gameObject.activeSelf)
                hide.Add(canvas.gameObject);
        overlay.hideWhileQuesting = hide.ToArray();
        view.questRoot.SetActive(false);
        ClearDebugLabels();
        CleanLaunchPlaceholders();
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(overlay);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return view;
    }

    public static QuestUIView BuildView()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(QuestTMPStabilizer.QuestFontPath);
        if (font == null) throw new InvalidOperationException("Quest Japanese TMP font is missing.");
        // Dynamic atlas keeps kanji added by future scenarios available; source TTF is bundled.
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        EditorUtility.SetDirty(font);
        forest = SpriteAt("Assets/Assets/Images/Backgrounds/Background.png");
        lily = SpriteAt("Assets/Image/Lily/Lily_Idle.png");

        RectTransform canvasRoot = Rect(null, "QuestCanvas", 0, 0, 1, 1);
        Canvas canvas = canvasRoot.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
        QuestUIView view = canvasRoot.gameObject.AddComponent<QuestUIView>();
        RectTransform launchSafe = Rect(canvasRoot, "LaunchSafeArea", 0, 0, 1, 1);
        launchSafe.gameObject.AddComponent<QuestSafeArea>();
        view.entry = canvasRoot.gameObject.AddComponent<QuestEntryController>();

        RectTransform screens = Rect(canvasRoot, "QuestUIRoot", 0, 0, 1, 1);
        view.questRoot = screens.gameObject;
        Image backdrop = Picture(screens, "ForestBackground", forest, 0, 0, 1, 1, false);
        backdrop.color = new Color(.94f, .98f, 1f, 1f);
        // A very light blue wash unifies the illustration without hiding the forest.
        Image wash = Picture(screens, "Atmosphere", null, 0, 0, 1, 1, false);
        wash.color = new Color(.06f,.15f,.25f,.12f);
        wash.raycastTarget = true;
        RectTransform safe = Rect(screens, "SafeArea", 0, 0, 1, 1);
        safe.gameObject.AddComponent<QuestSafeArea>();
        BuildChoice(view, safe);
        BuildVillager(view, safe);
        BuildChest(view, safe);
        BuildBattle(view, safe);
        BuildClear(view, safe);
        BuildHint(view, safe);
        // モーダルは最前面に描画するため最後に生成する（クエスト画面より後ろのシブリング）
        BuildEntryDialog(view, canvasRoot);
        return view;
    }

    /// <summary>
    /// 入口のモーダルダイアログ。旧「冒険クエスト」大型パネルの意匠（紺・金・クリーム、羅針盤の紋章、
    /// LILY'S ADVENTURE / 冒険クエスト の見出し、説明文、金色の主ボタン）をそのまま使う。
    /// 背景の暗幕は全画面、カード本体は SafeArea の中央に置く。
    /// </summary>
    private static void BuildEntryDialog(QuestUIView view, Transform canvasRoot)
    {
        RectTransform dialog = Rect(canvasRoot,"QuestEntryDialog",0,0,1,1);
        CanvasGroup group = dialog.gameObject.AddComponent<CanvasGroup>();
        Image dim = Picture(dialog,"DimOverlay",null,0,0,1,1,false);
        dim.color = new Color(.02f,.04f,.08f,.68f);
        dim.raycastTarget = true; // 背後の本編（タップ攻撃・ショップ等）への入力を遮る。誤操作防止のためタップしても閉じない
        RectTransform dialogSafe = Rect(dialog,"DialogSafeArea",0,0,1,1);
        dialogSafe.gameObject.AddComponent<QuestSafeArea>();

        RectTransform card = Surface(dialogSafe,"QuestDialogCard",.5f,.5f,.5f,.5f);
        card.sizeDelta = EntryDialogSize;
        card.anchoredPosition = Vector2.zero;
        card.GetComponent<QuestSurfaceGraphic>().raycastTarget = true;
        Emblem(card,"QuestSeal",QuestEmblemGraphic.Symbol.Compass,.055f,.765f,.19f,.95f);
        Label(card,"Eyebrow","LILY'S ADVENTURE",26,Gold,.23f,.855f,.92f,.94f,TextAlignmentOptions.Left);
        Label(card,"Title","冒険クエスト",54,Cream,.23f,.70f,.94f,.86f,TextAlignmentOptions.Left);
        var headline = Label(card,"HeadlineText","冒険クエスト解放まで",42,Cream,.06f,.575f,.94f,.675f);
        var progress = Label(card,"ProgressText","敵 0 / 10体",58,Gold,.06f,.435f,.94f,.56f);
        Slider bar = HpBar(card,"ProgressBar",.14f,.385f,.86f,.415f,Gold);
        bar.value = 0f;
        var hint = Label(card,"HintText","本編で敵を倒すと解放されます",30,Muted,.065f,.26f,.935f,.35f);
        var description = Label(card,"DescriptionText","森の奥から魔物の気配がする……",34,Muted,.065f,.44f,.935f,.555f);
        Button adventure = ActionButton(card,"AdventureButton","冒険に出る",.065f,.215f,.935f,.365f,true);
        Button close = ActionButton(card,"CloseButton","閉じる",.065f,.05f,.935f,.185f);

        view.openButton = adventure; // QuestOverlayController.openButton として配線される
        Set(view.entry,"dialogRoot",dialog.gameObject);
        Set(view.entry,"dialogGroup",group);
        Set(view.entry,"headlineText",headline);
        Set(view.entry,"progressText",progress);
        Set(view.entry,"progressBar",bar);
        Set(view.entry,"hintText",hint);
        Set(view.entry,"descriptionText",description);
        Set(view.entry,"adventureButton",adventure);
        Set(view.entry,"closeButton",close);
        description.gameObject.SetActive(false);
        adventure.gameObject.SetActive(false);
        dialog.gameObject.SetActive(false);
    }

    private static RectTransform Screen<T>(Transform parent, string name, out T controller) where T : Component
    {
        RectTransform host = Rect(parent, name, 0, 0, 1, 1);
        controller = host.gameObject.AddComponent<T>();
        RectTransform content = Rect(host,"Content",0,0,1,1);
        Set(controller,"root",content.gameObject);
        return content;
    }

    private static void Header(Transform parent, string eyebrow, string title)
    {
        RectTransform bar = Surface(parent,"ChapterHeader",.065f,.925f,.935f,.985f);
        Emblem(bar,"Mark",QuestEmblemGraphic.Symbol.Compass,.025f,.13f,.105f,.87f);
        Label(bar,"Chapter",eyebrow,25,Gold,.14f,.48f,.95f,.88f,TextAlignmentOptions.Left);
        Label(bar,"Subtitle",title,31,Cream,.14f,.06f,.95f,.51f,TextAlignmentOptions.Left);
    }

    private static void DialogueHeader(Transform parent)
    {
        // 幅はSafeAreaに追従し、高さ・行間はCanvasの基準サイズで一定に保つ。
        RectTransform bar = Surface(parent,"ChapterHeader",.065f,1f,.935f,1f);
        bar.pivot = new Vector2(.5f,1f);
        bar.anchoredPosition = new Vector2(0f,-24f);
        bar.sizeDelta = new Vector2(0f,200f);
        Emblem(bar,"Mark",QuestEmblemGraphic.Symbol.Compass,.5f,1f,.5f,1f);
        RectTransform mark = (RectTransform)bar.Find("Mark");
        mark.pivot = new Vector2(.5f,1f);
        mark.anchoredPosition = new Vector2(0f,-16f);
        mark.sizeDelta = new Vector2(72f,72f);
        var chapter = Label(bar,"Chapter","A MEETING IN THE WOODS",25,Gold,.06f,1f,.94f,1f);
        chapter.rectTransform.pivot = new Vector2(.5f,1f);
        chapter.rectTransform.anchoredPosition = new Vector2(0f,-98f);
        chapter.rectTransform.sizeDelta = new Vector2(0f,34f);
        var subtitle = Label(bar,"Subtitle","森での出会い",31,Cream,.06f,1f,.94f,1f);
        subtitle.rectTransform.pivot = new Vector2(.5f,1f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f,-140f);
        subtitle.rectTransform.sizeDelta = new Vector2(0f,42f);
        DialogueHeaderLayout.Apply(bar);
    }

    private static void BuildChoice(QuestUIView view, Transform parent)
    {
        RectTransform content = Screen(parent,"QuestChoicePanel",out QuestChoicePanel panel);
        view.choicePanel = panel;
        Header(content,"THE ENCHANTED FOREST","魔法の森を進もう");
        var header = Label(content,"HeaderText","どちらの道を進む？",56,Ink,.06f,.82f,.94f,.90f);
        var step = Label(content,"StepText","1つ目の分岐 / あと3イベント",30,Ink,.10f,.775f,.90f,.82f);
        Set(panel,"headerText",header);Set(panel,"stepText",step);
        for(int side=0;side<2;side++)
        {
            float x = side==0 ? .06f : .52f;
            RectTransform card=Surface(content,side==0?"LeftCard":"RightCard",x,.30f,x+.42f,.755f);
            Button button = card.gameObject.AddComponent<Button>(); StyleButton(button,card.GetComponent<QuestSurfaceGraphic>());
            RectTransform artFrame=Surface(card,"IllustrationFrame",.055f,.39f,.945f,.945f);
            artFrame.GetComponent<QuestSurfaceGraphic>().cornerRadius=20;
            Image art=Picture(artFrame,"PathImage",forest,.025f,.025f,.975f,.975f,false);
            art.color=side==0?Color.white:new Color(.83f,.9f,1f,1f);
            var name=Label(card,"PathTitle",side==0?"森の小道":"古い街道",44,Cream,.06f,.245f,.94f,.375f);
            var desc=Label(card,"PathDescription",side==0?"静かな道が\n続いている":"何か\n見つかりそうだ",31,Muted,.07f,.11f,.93f,.25f);
            Label(card,"ChooseCaption","この道を進む  ›",29,Gold,.08f,.025f,.92f,.10f);
            string prefix=side==0?"left":"right";
            Set(panel,prefix+"Button",button);Set(panel,prefix+"Icon",art);Set(panel,prefix+"Label",name);Set(panel,prefix+"DescriptionText",desc);
        }
        Picture(content,"LilyGuideImage",lily,.38f,.055f,.64f,.285f);
        content.gameObject.SetActive(false);
    }

    private static void BuildVillager(QuestUIView view, Transform parent)
    {
        RectTransform content=Screen(parent,"VillagerEventPanel",out VillagerEventPanel panel);view.villagerPanel=panel;
        DialogueHeader(content);
        Image npc=Slot(content,"NPCImage",null,QuestEmblemGraphic.Symbol.Portrait,.165f,.47f,.835f,.85f);
        Set(panel,"villagerImage",npc);
        RectTransform dialog=Surface(content,"DialogueWindow",.06f,.30f,.94f,.535f);
        var speaker=Label(dialog,"SpeakerName","旅のひと",40,Gold,.06f,.73f,.94f,.94f,TextAlignmentOptions.Left);
        var message=Label(dialog,"DialogueText","",38,Cream,.06f,.16f,.94f,.72f,TextAlignmentOptions.TopLeft);
        var prompt=Label(dialog,"TapPrompt","タップして続きを読む",27,Muted,.06f,.035f,.94f,.17f,TextAlignmentOptions.Right);
        Button advance=dialog.gameObject.AddComponent<Button>();StyleButton(advance,dialog.GetComponent<QuestSurfaceGraphic>());
        Set(panel,"speakerNameText",speaker);Set(panel,"dialogueText",message);Set(panel,"tapPrompt",prompt.gameObject);Set(panel,"advanceButton",advance);
        RectTransform choices=Rect(content,"ChoiceGroup",.07f,.085f,.93f,.278f);
        Button a=ActionButton(choices,"ChoiceA","話を聞く",0,.54f,1,1,true);
        Button b=ActionButton(choices,"ChoiceB","先を急ぐ",0,0,1,.46f);
        Set(panel,"choiceGroup",choices.gameObject);Set(panel,"choiceButtonA",a);Set(panel,"choiceLabelA",a.GetComponentInChildren<TextMeshProUGUI>());
        Set(panel,"choiceButtonB",b);Set(panel,"choiceLabelB",b.GetComponentInChildren<TextMeshProUGUI>());
        ResultBox(content,panel,.07f,.09f,.93f,.42f,"旅の出会い");
        content.gameObject.SetActive(false);
    }

    private static void BuildChest(QuestUIView view,Transform parent)
    {
        RectTransform content=Screen(parent,"ChestEventPanel",out ChestEventPanel panel);view.chestPanel=panel;
        Header(content,"A HIDDEN TREASURE","森に眠る宝物");
        Label(content,"ChestTitle","宝箱を見つけた！",54,Ink,.07f,.79f,.93f,.87f);
        Image chest=Slot(content,"ChestImage",null,QuestEmblemGraphic.Symbol.Chest,.20f,.46f,.80f,.77f);
        Set(panel,"chestImage",chest);
        Label(content,"ChestCaption","何が入っているだろう？",35,Ink,.10f,.39f,.90f,.45f);
        Button open=ActionButton(content,"OpenChestButton","宝箱を開ける",.14f,.265f,.86f,.36f,true);
        Set(panel,"openButton",open);Set(panel,"openButtonText",open.GetComponentInChildren<TextMeshProUGUI>());
        RectTransform result=Surface(content,"ResultBox",.065f,.075f,.935f,.43f);
        var rarity=Label(result,"RarityText","TREASURE",29,Gold,.06f,.83f,.94f,.94f);
        var item=Label(result,"ItemNameText","森の宝箱",44,Cream,.06f,.65f,.94f,.82f);
        var desc=Label(result,"DescriptionText","",30,Muted,.07f,.49f,.93f,.65f);
        var body=Label(result,"ResultText","",31,Cream,.07f,.295f,.93f,.49f);
        Button ok=ActionButton(result,"ContinueButton","冒険を続ける",.07f,.045f,.93f,.285f,true);
        Set(panel,"resultBox",result.gameObject);Set(panel,"rarityText",rarity);Set(panel,"itemNameText",item);Set(panel,"descriptionText",desc);Set(panel,"resultText",body);Set(panel,"okButton",ok);
        // sparkleEffect intentionally remains optional and unassigned.
        result.gameObject.SetActive(false);content.gameObject.SetActive(false);
    }

    private static void BuildBattle(QuestUIView view,Transform parent)
    {
        RectTransform content=Screen(parent,"BattlePanel",out BattlePanel panel);view.battlePanel=panel;
        QuestBattlePresentation display=panel.gameObject.AddComponent<QuestBattlePresentation>();Set(panel,"presentation",display);
        Header(content,"BATTLE","リリィと一緒に戦おう");
        RectTransform boss=Surface(content,"BossLabel",.35f,.875f,.65f,.918f);
        Label(boss,"BossCaption","BOSS",32,Gold,0,0,1,1);Set(panel,"bossLabel",boss.gameObject);
        var enemyName=Label(content,"EnemyNameText","森のまもの",45,Ink,.06f,.824f,.94f,.883f);Set(panel,"enemyNameText",enemyName);
        Sprite regular=SpriteAt("Assets/Image/Enemy/18.png");
        Sprite bossSprite=SpriteAt("Assets/Image/Enemy/23.png");
        Image enemy=Slot(content,"EnemyImage",regular,QuestEmblemGraphic.Symbol.Star,.255f,.64f,.745f,.825f);
        Set(panel,"enemyImage",enemy);Set(panel,"defaultEnemySprite",regular);Set(panel,"bossSprite",bossSprite);
        Slider hp=HpBar(content,"EnemyHpBar",.20f,.618f,.80f,.631f,Hex("D6A86E"));
        var hpText=Label(content,"EnemyHpText","HP 10 / 10",31,Ink,.10f,.575f,.90f,.615f);
        Set(panel,"hpSlider",hp);Set(panel,"hpText",hpText);Set(display,"enemyHpText",hpText);Set(display,"enemyHpBar",hp.GetComponent<RectTransform>());

        RectTransform quiz=Surface(content,"WordQuestionCard",.06f,.285f,.94f,.565f);
        var english=Label(quiz,"EnglishText","sword",79,Cream,.05f,.59f,.95f,.95f);
        var reading=Label(quiz,"ReadingText","ソード",28,Muted,.05f,.50f,.95f,.62f);
        var question=Label(quiz,"QuestionPrompt","意味を選んでください",33,Gold,.05f,.365f,.95f,.51f);
        Button answerA=ActionButton(quiz,"AnswerA","剣",.05f,.045f,.48f,.345f,true);
        Button answerB=ActionButton(quiz,"AnswerB","盾",.52f,.045f,.95f,.345f,true);
        Set(panel,"englishText",english);Set(panel,"readingText",reading);
        Set(display,"questionPrompt",question);Set(display,"answerAButton",answerA);Set(display,"answerAText",answerA.GetComponentInChildren<TextMeshProUGUI>());
        Set(display,"answerBButton",answerB);Set(display,"answerBText",answerB.GetComponentInChildren<TextMeshProUGUI>());
        var feedback=Label(content,"AnswerFeedback","正しい意味を選んで攻撃しよう",29,Ink,.06f,.247f,.94f,.282f);Set(display,"feedbackText",feedback);

        RectTransform player=Surface(content,"LilyStatusCard",.06f,.08f,.94f,.23f);
        Picture(player,"LilyImage",lily,.015f,.02f,.32f,1.23f);
        var name=Label(player,"LilyName","Lily  Lv.1",42,Cream,.34f,.64f,.94f,.92f,TextAlignmentOptions.Left);
        var turn=Label(player,"TurnText","Lily のターン",27,Gold,.34f,.45f,.94f,.65f,TextAlignmentOptions.Left);
        Slider playerHp=HpBar(player,"LilyHpBar",.34f,.28f,.93f,.37f,Hex("8DBBBB"));
        var playerHpText=Label(player,"LilyHpText","HP 100 / 100",30,Cream,.34f,.07f,.94f,.27f,TextAlignmentOptions.Left);
        Set(display,"lilyNameText",name);Set(display,"turnText",turn);Set(display,"lilyHpSlider",playerHp);Set(display,"lilyHpText",playerHpText);
        // Serialized legacy input is kept for the battle API; its view is hidden by the adapter.
        Button legacy=ActionButton(content,"BattleAttackInput","攻撃！",.12f,.34f,.88f,.46f,true);
        CanvasGroup legacyGroup=legacy.gameObject.AddComponent<CanvasGroup>();legacyGroup.alpha=1;legacyGroup.interactable=true;legacyGroup.blocksRaycasts=true;
        Set(panel,"attackButton",legacy);Set(panel,"attackButtonText",legacy.GetComponentInChildren<TextMeshProUGUI>());
        var floating=Label(content,"DamageFloat","",46,Gold,.50f,.68f,.88f,.76f);Set(panel,"damageFloatText",floating);
        ResultBox(content,panel,.075f,.29f,.925f,.61f,"BATTLE RESULT");
        BattleCommandUIBuilder.Install(panel);
        quiz.gameObject.SetActive(false);
        feedback.gameObject.SetActive(false);
        content.gameObject.SetActive(false);
    }

    private static void BuildClear(QuestUIView view,Transform parent)
    {
        RectTransform content=Screen(parent,"QuestClearPanel",out QuestClearPanel panel);view.clearPanel=panel;
        Header(content,"ADVENTURE COMPLETE","冒険の記録");
        RectTransform card=Surface(content,"QuestResultCard",.065f,.14f,.935f,.875f);
        var title=Label(card,"TitleText","QUEST CLEAR!",57,Gold,.055f,.845f,.945f,.945f);
        var questName=Label(card,"QuestNameText","はじめての冒険",34,Cream,.07f,.785f,.93f,.85f);
        Slot(card,"RewardIconImage",SpriteAt("Assets/Resources/Chest/open_rare.png"),QuestEmblemGraphic.Symbol.Chest,.29f,.56f,.71f,.775f);
        var message=Label(card,"ClearMessageText","見事な冒険だった！",35,Cream,.08f,.45f,.92f,.575f);
        RectTransform reward=Surface(card,"RewardSummary",.10f,.245f,.90f,.435f);
        reward.GetComponent<QuestSurfaceGraphic>().borderColor=new Color(.85f,.76f,.54f,.4f);
        Picture(reward,"GoldIcon",SpriteAt("Assets/Image/UI/Coin.png"),.05f,.52f,.20f,.92f);
        Picture(reward,"ExpIcon",SpriteAt("Assets/Image/UI/Star.png"),.05f,.07f,.20f,.47f);
        var gold=Label(reward,"GoldText","Gold +0",46,Gold,.25f,.50f,.95f,.94f,TextAlignmentOptions.Left);
        var exp=Label(reward,"ExpText","EXP +0",38,Cream,.25f,.065f,.95f,.49f,TextAlignmentOptions.Left);
        var item=Label(card,"ItemText","",29,Muted,.10f,.19f,.90f,.24f);
        Button close=ActionButton(card,"FinishAdventureButton","冒険を終える",.09f,.055f,.91f,.175f,true);
        Set(panel,"titleText",title);Set(panel,"questNameText",questName);Set(panel,"clearMessageText",message);
        Set(panel,"goldText",gold);Set(panel,"expText",exp);Set(panel,"itemText",item);Set(panel,"closeButton",close);Set(panel,"closeButtonLabel",close.GetComponentInChildren<TextMeshProUGUI>());
        content.gameObject.SetActive(false);
    }

    private static void BuildHint(QuestUIView view,Transform parent)
    {
        RectTransform host=Rect(parent,"PipHintController",0,0,1,1);PipHintController controller=host.gameObject.AddComponent<PipHintController>();view.pipHint=controller;
        RectTransform root=Rect(host,"PipHint",.09f,.012f,.91f,.067f);
        RectTransform bubble=Surface(root,"SpeechBubble",0,0,1,1);
        bubble.GetComponent<QuestSurfaceGraphic>().borderWidth=1f;
        var label=Label(bubble,"HintText","",25,Muted,.05f,.07f,.95f,.93f);
        Set(controller,"root",root.gameObject);Set(controller,"speechBubble",bubble.gameObject);Set(controller,"hintText",label);
        Set(view.clearPanel,"pipHint",controller);
        root.gameObject.SetActive(false);
    }

    private static void ResultBox(Transform parent,Component panel,float x0,float y0,float x1,float y1,string title)
    {
        RectTransform box=Surface(parent,"ResultBox",x0,y0,x1,y1);
        Label(box,"ResultTitle",title,30,Gold,.06f,.76f,.94f,.94f);
        var body=Label(box,"ResultText","",40,Cream,.07f,.36f,.93f,.76f);
        Button ok=ActionButton(box,"ResultConfirmButton","冒険を続ける",.075f,.065f,.925f,.345f,true);
        Set(panel,"resultBox",box.gameObject);Set(panel,"resultText",body);Set(panel,"okButton",ok);
        box.gameObject.SetActive(false);
    }

    private static RectTransform Rect(Transform parent,string name,float x0,float y0,float x1,float y1)
    {
        GameObject go=new GameObject(name,typeof(RectTransform));go.layer=5;
        RectTransform rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=new Vector2(x0,y0);rt.anchorMax=new Vector2(x1,y1);rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
        return rt;
    }
    private static RectTransform Surface(Transform parent,string name,float x0,float y0,float x1,float y1)
    {
        RectTransform rt=Rect(parent,name,x0,y0,x1,y1);var graphic=rt.gameObject.AddComponent<QuestSurfaceGraphic>();graphic.raycastTarget=false;return rt;
    }
    private static TextMeshProUGUI Label(Transform parent,string name,string value,float size,Color color,float x0,float y0,float x1,float y1,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
    {
        var text=Rect(parent,name,x0,y0,x1,y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;
        text.enableAutoSizing=true;text.fontSizeMax=size;text.fontSizeMin=size*.80f;
        text.raycastTarget=false;return text;
    }
    private static Image Picture(Transform parent,string name,Sprite sprite,float x0,float y0,float x1,float y1,bool preserve=true)
    {
        var image=Rect(parent,name,x0,y0,x1,y1).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=preserve;image.raycastTarget=false;return image;
    }
    private static Image Slot(Transform parent,string name,Sprite sprite,QuestEmblemGraphic.Symbol symbol,float x0,float y0,float x1,float y1)
    {
        RectTransform frame=Rect(parent,name+"Slot",x0,y0,x1,y1);
        RectTransform fallback=Surface(frame,"FallbackMedallion",.04f,.04f,.96f,.96f);
        QuestSurfaceGraphic surface=fallback.GetComponent<QuestSurfaceGraphic>();surface.cornerRadius=300f;surface.topColor=new Color(.10f,.22f,.32f,.87f);surface.bottomColor=new Color(.07f,.14f,.23f,.91f);
        Emblem(fallback,"GoldEmblem",symbol,.13f,.13f,.87f,.87f);
        Image image=Picture(frame,name,sprite,0,0,1,1);
        QuestSpriteSlot slot=frame.gameObject.AddComponent<QuestSpriteSlot>();slot.image=image;slot.fallback=fallback.gameObject;slot.Refresh();return image;
    }
    private static void Emblem(Transform parent,string name,QuestEmblemGraphic.Symbol symbol,float x0,float y0,float x1,float y1)
    {
        var icon=Rect(parent,name,x0,y0,x1,y1).gameObject.AddComponent<QuestEmblemGraphic>();icon.symbol=symbol;icon.color=Gold;icon.raycastTarget=false;
    }
    private static Button ActionButton(Transform parent,string name,string label,float x0,float y0,float x1,float y1,bool primary=false)
    {
        RectTransform rect=Surface(parent,name,x0,y0,x1,y1);QuestSurfaceGraphic face=rect.GetComponent<QuestSurfaceGraphic>();
        face.cornerRadius=24;
        if(primary){face.topColor=Hex("F0D8A0");face.bottomColor=Hex("CDA970");face.borderColor=Hex("F9E9BD");}
        Button button=rect.gameObject.AddComponent<Button>();StyleButton(button,face);
        Label(rect,"Label",label,43,primary?Ink:Cream,.055f,.07f,.945f,.93f);return button;
    }
    private static void StyleButton(Button button,Graphic face)
    {
        face.raycastTarget=true;button.targetGraphic=face;
        ColorBlock colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.96f,.84f);colors.pressedColor=new Color(.76f,.82f,.91f);colors.selectedColor=Color.white;colors.disabledColor=new Color(.6f,.64f,.7f,.8f);colors.fadeDuration=.12f;button.colors=colors;
        Navigation navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
    }
    private static Slider HpBar(Transform parent,string name,float x0,float y0,float x1,float y1,Color fillColor)
    {
        RectTransform rt=Surface(parent,name,x0,y0,x1,y1);var bg=rt.GetComponent<QuestSurfaceGraphic>();bg.cornerRadius=12;bg.borderWidth=2;bg.topColor=Ink;bg.bottomColor=Ink;
        RectTransform area=Rect(rt,"FillArea",0,0,1,1);area.offsetMin=new Vector2(4,4);area.offsetMax=new Vector2(-4,-4);
        RectTransform fill=Surface(area,"Fill",0,0,1,1);var face=fill.GetComponent<QuestSurfaceGraphic>();face.cornerRadius=9;face.borderWidth=0;face.topColor=fillColor;face.bottomColor=fillColor*.82f;
        Slider slider=rt.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.minValue=0;slider.maxValue=1;slider.value=1;slider.interactable=false;return slider;
    }
    private static Sprite SpriteAt(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }
    private static void Set(Object target,string property,Object value)
    {
        SerializedObject so=new SerializedObject(target);SerializedProperty p=so.FindProperty(property);
        if(p==null)throw new InvalidOperationException(target.GetType().Name+" missing UI field "+property);
        p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Color Hex(string value)
    {
        Color c;ColorUtility.TryParseHtmlString("#"+value,out c);return c;
    }
    private static Dictionary<string,Object> CaptureArt(QuestManager manager)
    {
        var art=new Dictionary<string,Object>();
        Capture(art,"battle",manager.battlePanel,new[]{"defaultEnemySprite","bossSprite"});
        Capture(art,"villager",manager.villagerPanel,new[]{"defaultVillagerSprite"});
        Capture(art,"chest",manager.chestPanel,new[]{"chestClosed","chestOpenNormal","chestOpenMiss","chestOpenRare","sparkleEffect"});
        Capture(art,"clear",manager.clearPanel,new[]{"lilyHappySprite","lilySadSprite","clearSE","gameOverSE"});
        return art;
    }
    private static void Capture(Dictionary<string,Object> art,string prefix,Object panel,string[] fields)
    {
        if(panel==null)return;var so=new SerializedObject(panel);
        foreach(string field in fields){var p=so.FindProperty(field);if(p!=null&&p.objectReferenceValue!=null&&EditorUtility.IsPersistent(p.objectReferenceValue))art[prefix+"/"+field]=p.objectReferenceValue;}
    }
    private static void RestoreArt(QuestManager manager,Dictionary<string,Object> art)
    {
        foreach(var pair in art)
        {
            string[] key=pair.Key.Split('/');Object panel=key[0]=="battle"?(Object)manager.battlePanel:key[0]=="villager"?manager.villagerPanel:key[0]=="chest"?manager.chestPanel:manager.clearPanel;
            Set(panel,key[1],pair.Value);
        }
    }
    private static T[] InScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(component => component.gameObject.scene == scene).ToArray();
    }
    private static Dictionary<string, Sprite> CaptureImages(QuestUIView view)
    {
        var images = new Dictionary<string, Sprite>();
        if (view == null) return images;
        foreach (Image image in view.GetComponentsInChildren<Image>(true))
            images[AnimationUtility.CalculateTransformPath(image.transform, view.transform)] = image.sprite;
        return images;
    }
    private static void RestoreImages(QuestUIView view, Dictionary<string, Sprite> images)
    {
        foreach (var pair in images)
        {
            Transform target = view.transform.Find(pair.Key);
            if (target == null) continue;
            Image image = target.GetComponent<Image>();
            if (image == null) continue;
            image.sprite = pair.Value;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
    }
    private static void ClearDebugLabels()
    {
        foreach(Text text in InScene<Text>())
            if(text.text=="Text"||text.text=="New Text"||text.text.Contains("クエスト開始（テスト）")){text.text="";EditorUtility.SetDirty(text);}
        foreach(TMP_Text text in InScene<TMP_Text>())
            if(text.text=="Text"||text.text=="New Text"||text.text.Contains("クエスト開始（テスト）")){text.text="";EditorUtility.SetDirty(text);}
    }
    private static void CleanLaunchPlaceholders()
    {
        // 本編への参照を残したまま、開始カードの背後に見える仮の描画だけを整理する。
        foreach (Image image in InScene<Image>())
        {
            Transform parent = image.transform.parent;
            if (parent == null || parent.name != "PnlSafe") continue;
            if (image.name == "EnemySpawner" && image.sprite == null)
            {
                Color color = image.color;
                color.a = 0f;
                image.color = color;
                EditorUtility.SetDirty(image);
            }
            else if ((image.name == "Icon_Gold" || image.name == "Icon_Score") && image.sprite == null)
            {
                image.sprite = SpriteAt(image.name == "Icon_Gold" ? "Assets/Image/UI/Coin.png" : "Assets/Image/UI/Star.png");
                image.preserveAspect = true;
                EditorUtility.SetDirty(image);
            }
            // 本編 HUD（Text_Gold など）の配置はシーン側が持つ。ここで座標を書き込むと
            // Builder を再実行するたびにシーンでの調整が固定値へ戻ってしまうため、画像の補完だけ行う。
        }
    }
}
