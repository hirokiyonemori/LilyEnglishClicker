using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// メニュー「LilyClicker > Quest UI を自動生成」で
/// Canvas 内にクエスト用 UI をすべて組み立てるエディタスクリプト
/// </summary>
public class QuestSceneSetup : EditorWindow
{
    /// <summary>Build() が生成したオブジェクト一式</summary>
    public class Result
    {
        public GameObject managerObj;   // QuestManager
        public GameObject questRoot;    // Canvas 配下の QuestUIRoot
        public Button startButton;      // DEBUG_StartQuestButton
    }

    [MenuItem("LilyClicker/Quest UI を自動生成")]
    static void CreateQuestUI()
    {
        Build();
    }

    [MenuItem("LilyClicker/GameシーンにQuestオーバーレイを組み込む")]
    static void InstallIntoGameScene()
    {
        const string gameScenePath = "Assets/Scenes/Game.unity";

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(gameScenePath);
        }
        else
        {
            return;
        }

        foreach (QuestManager existingManager in Object.FindObjectsOfType<QuestManager>(true))
        {
            Object.DestroyImmediate(existingManager.gameObject);
        }

        foreach (QuestOverlayController existingOverlay in Object.FindObjectsOfType<QuestOverlayController>(true))
        {
            Object.DestroyImmediate(existingOverlay.gameObject);
        }

        GameObject existingRoot = GameObject.Find("QuestUIRoot");
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        Result result = Build();

        if (result.startButton != null)
        {
            Object.DestroyImmediate(result.startButton.gameObject);
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        Button gameStartButton = CreateButton(canvas.transform, "QuestStartButton", "クエスト",
            Vector2.zero, new Color(0.1f, 0.7f, 0.1f));
        ConfigureGameStartButton(gameStartButton);

        QuestOverlayController overlay = result.managerObj.AddComponent<QuestOverlayController>();
        SerializedObject overlaySO = new SerializedObject(overlay);
        overlaySO.FindProperty("questRoot").objectReferenceValue = result.questRoot;
        overlaySO.FindProperty("openButton").objectReferenceValue = gameStartButton;
        overlaySO.ApplyModifiedProperties();

        // 実行時は QuestOverlayController.Start が閉じるが、
        // 編集中もSceneビューを覆わないようここで畳んでおく
        result.questRoot.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=green>★ GameシーンにQuestオーバーレイを組み込みました。</color>");
    }

    /// <summary>
    /// 現在開いているシーンにクエストUI一式を組み立てる。
    /// InstallIntoGameScene（オーバーレイ組み込み）からも呼ばれる
    /// </summary>
    public static Result Build()
    {
        // ─── Canvas を探す or 作成 ─────────────────────────────
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // ─── QuestManager を作成 ────────────────────────────────
        GameObject managerObj = new GameObject("QuestManager");
        QuestManager qm = managerObj.AddComponent<QuestManager>();
        Undo.RegisterCreatedObjectUndo(managerObj, "Create QuestManager");

        // ─── メインの QuestUIRoot ────────────────────────────────
        GameObject questRoot = CreateChild(canvas.transform, "QuestUIRoot");
        RectTransform rootRT = questRoot.GetComponent<RectTransform>();
        StretchFull(rootRT);

        // ================================================================
        //  1) QuestChoicePanel
        // ================================================================
        GameObject choicePanel = CreatePanel(questRoot.transform, "QuestChoicePanel", new Color(0.1f, 0.15f, 0.3f, 0.9f));
        QuestChoicePanel choiceScript = choicePanel.AddComponent<QuestChoicePanel>();

        // ChoicePanel の子要素
        TextMeshProUGUI choiceHeader = CreateTMP(choicePanel.transform, "HeaderText", "どちらの道を選ぶ？",
            36, TextAlignmentOptions.Center, new Vector2(0, 160));
        TextMeshProUGUI choiceStep = CreateTMP(choicePanel.transform, "StepText", "【1つ目の分岐】あと3イベント",
            22, TextAlignmentOptions.Center, new Vector2(0, 110));

        // 左カード
        GameObject leftCard = CreateChild(choicePanel.transform, "LeftCard");
        SetRect(leftCard, new Vector2(-160, -20), new Vector2(240, 280));
        Image leftBg = leftCard.AddComponent<Image>();
        leftBg.color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
        Image leftIcon = CreateImageChild(leftCard.transform, "Icon", null, new Vector2(0, 30), new Vector2(100, 100));
        TextMeshProUGUI leftLabel = CreateTMP(leftCard.transform, "Label", "左の道",
            24, TextAlignmentOptions.Center, new Vector2(0, -80));
        Button leftBtn = leftCard.AddComponent<Button>();
        leftBtn.targetGraphic = leftBg;

        // 右カード
        GameObject rightCard = CreateChild(choicePanel.transform, "RightCard");
        SetRect(rightCard, new Vector2(160, -20), new Vector2(240, 280));
        Image rightBg = rightCard.AddComponent<Image>();
        rightBg.color = new Color(0.6f, 0.3f, 0.2f, 0.8f);
        Image rightIcon = CreateImageChild(rightCard.transform, "Icon", null, new Vector2(0, 30), new Vector2(100, 100));
        TextMeshProUGUI rightLabel = CreateTMP(rightCard.transform, "Label", "右の道",
            24, TextAlignmentOptions.Center, new Vector2(0, -80));
        Button rightBtn = rightCard.AddComponent<Button>();
        rightBtn.targetGraphic = rightBg;

        // ChoicePanel にアタッチ
        SerializedObject choiceSO = new SerializedObject(choiceScript);
        choiceSO.FindProperty("root").objectReferenceValue        = choicePanel;
        choiceSO.FindProperty("headerText").objectReferenceValue  = choiceHeader;
        choiceSO.FindProperty("stepText").objectReferenceValue    = choiceStep;
        choiceSO.FindProperty("leftButton").objectReferenceValue  = leftBtn;
        choiceSO.FindProperty("leftIcon").objectReferenceValue    = leftIcon;
        choiceSO.FindProperty("leftLabel").objectReferenceValue   = leftLabel;
        choiceSO.FindProperty("rightButton").objectReferenceValue = rightBtn;
        choiceSO.FindProperty("rightIcon").objectReferenceValue   = rightIcon;
        choiceSO.FindProperty("rightLabel").objectReferenceValue  = rightLabel;
        choiceSO.ApplyModifiedProperties();

        // ================================================================
        //  2) VillagerEventPanel
        // ================================================================
        GameObject villagerPanel = CreatePanel(questRoot.transform, "VillagerEventPanel", new Color(0.15f, 0.25f, 0.15f, 0.9f));
        VillagerEventPanel villagerScript = villagerPanel.AddComponent<VillagerEventPanel>();

        Image villagerImg = CreateImageChild(villagerPanel.transform, "VillagerImage", null,
            new Vector2(-140, 60), new Vector2(160, 200));
        villagerImg.color = new Color(0.8f, 0.7f, 0.5f);
        TextMeshProUGUI speakerName = CreateTMP(villagerPanel.transform, "SpeakerName", "老人",
            24, TextAlignmentOptions.Left, new Vector2(40, 140));
        TextMeshProUGUI dialogue = CreateTMP(villagerPanel.transform, "DialogueText", "",
            22, TextAlignmentOptions.Left, new Vector2(40, 50));
        SetSize(dialogue, 320, 160);
        GameObject tapPrompt = CreateChild(villagerPanel.transform, "TapPrompt");
        SetRect(tapPrompt, new Vector2(0, -140), new Vector2(200, 40));
        TextMeshProUGUI tapText = tapPrompt.AddComponent<TextMeshProUGUI>();
        tapText.text = "▼タップで次へ"; tapText.fontSize = 18;
        tapText.alignment = TextAlignmentOptions.Center; tapText.color = Color.yellow;

        // 選択肢
        GameObject choiceGrp = CreateChild(villagerPanel.transform, "ChoiceGroup");
        SetRect(choiceGrp, new Vector2(0, -80), new Vector2(500, 80));
        Button btnA = CreateButton(choiceGrp.transform, "ChoiceButtonA", "話を聞く", new Vector2(-130, 0), new Color(0.2f, 0.5f, 0.3f));
        Button btnB = CreateButton(choiceGrp.transform, "ChoiceButtonB", "無視する", new Vector2(130, 0), new Color(0.5f, 0.3f, 0.2f));
        TextMeshProUGUI lblA = btnA.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI lblB = btnB.GetComponentInChildren<TextMeshProUGUI>();

        // 結果
        GameObject vResultBox = CreateResultBox(villagerPanel.transform, "ResultBox", out TextMeshProUGUI vResultText, out Button vOkBtn);

        SerializedObject villagerSO = new SerializedObject(villagerScript);
        villagerSO.FindProperty("root").objectReferenceValue            = villagerPanel;
        villagerSO.FindProperty("villagerImage").objectReferenceValue   = villagerImg;
        villagerSO.FindProperty("speakerNameText").objectReferenceValue = speakerName;
        villagerSO.FindProperty("dialogueText").objectReferenceValue    = dialogue;
        villagerSO.FindProperty("tapPrompt").objectReferenceValue       = tapPrompt;
        villagerSO.FindProperty("choiceGroup").objectReferenceValue     = choiceGrp;
        villagerSO.FindProperty("choiceButtonA").objectReferenceValue   = btnA;
        villagerSO.FindProperty("choiceLabelA").objectReferenceValue    = lblA;
        villagerSO.FindProperty("choiceButtonB").objectReferenceValue   = btnB;
        villagerSO.FindProperty("choiceLabelB").objectReferenceValue    = lblB;
        villagerSO.FindProperty("resultBox").objectReferenceValue       = vResultBox;
        villagerSO.FindProperty("resultText").objectReferenceValue      = vResultText;
        villagerSO.FindProperty("okButton").objectReferenceValue        = vOkBtn;
        villagerSO.ApplyModifiedProperties();

        // ================================================================
        //  3) ChestEventPanel
        // ================================================================
        GameObject chestPanel = CreatePanel(questRoot.transform, "ChestEventPanel", new Color(0.3f, 0.2f, 0.1f, 0.9f));
        ChestEventPanel chestScript = chestPanel.AddComponent<ChestEventPanel>();

        Image chestImg = CreateImageChild(chestPanel.transform, "ChestImage", null,
            new Vector2(0, 40), new Vector2(180, 180));
        chestImg.color = new Color(0.8f, 0.6f, 0.2f);

        Button openBtn = CreateButton(chestPanel.transform, "OpenButton", "宝箱を開ける！", new Vector2(0, -80), new Color(0.9f, 0.7f, 0.1f));
        TextMeshProUGUI openLabel = openBtn.GetComponentInChildren<TextMeshProUGUI>();

        // 結果
        GameObject cResultBox = CreateResultBox(chestPanel.transform, "ResultBox", out TextMeshProUGUI cResultText, out Button cOkBtn);
        TextMeshProUGUI rarityText = CreateTMP(cResultBox.transform, "RarityText", "",
            28, TextAlignmentOptions.Center, new Vector2(0, 60));

        SerializedObject chestSO = new SerializedObject(chestScript);
        chestSO.FindProperty("root").objectReferenceValue           = chestPanel;
        chestSO.FindProperty("chestImage").objectReferenceValue     = chestImg;
        chestSO.FindProperty("openButton").objectReferenceValue     = openBtn;
        chestSO.FindProperty("openButtonText").objectReferenceValue = openLabel;
        chestSO.FindProperty("resultBox").objectReferenceValue      = cResultBox;
        chestSO.FindProperty("rarityText").objectReferenceValue     = rarityText;
        chestSO.FindProperty("resultText").objectReferenceValue     = cResultText;
        chestSO.FindProperty("okButton").objectReferenceValue       = cOkBtn;
        chestSO.ApplyModifiedProperties();

        // ================================================================
        //  4) BattlePanel
        // ================================================================
        GameObject battlePanelObj = CreatePanel(questRoot.transform, "BattlePanel", new Color(0.2f, 0.1f, 0.1f, 0.9f));
        BattlePanel battleScript = battlePanelObj.AddComponent<BattlePanel>();

        Image enemyImg = CreateImageChild(battlePanelObj.transform, "EnemyImage", null,
            new Vector2(0, 80), new Vector2(180, 180));
        enemyImg.color = new Color(0.6f, 0.2f, 0.2f);

        TextMeshProUGUI enemyName = CreateTMP(battlePanelObj.transform, "EnemyNameText", "まもの",
            28, TextAlignmentOptions.Center, new Vector2(0, 200));
        TextMeshProUGUI engText = CreateTMP(battlePanelObj.transform, "EnglishText", "dragon",
            40, TextAlignmentOptions.Center, new Vector2(0, -10));
        engText.fontStyle = FontStyles.Bold;
        TextMeshProUGUI rdText = CreateTMP(battlePanelObj.transform, "ReadingText", "ドラゴン",
            22, TextAlignmentOptions.Center, new Vector2(0, -45));

        // ボスラベル
        GameObject bossLbl = CreateChild(battlePanelObj.transform, "BossLabel");
        SetRect(bossLbl, new Vector2(0, 240), new Vector2(200, 50));
        TextMeshProUGUI bossLblText = bossLbl.AddComponent<TextMeshProUGUI>();
        bossLblText.text = "BOSS"; bossLblText.fontSize = 36; bossLblText.fontStyle = FontStyles.Bold;
        bossLblText.alignment = TextAlignmentOptions.Center; bossLblText.color = Color.red;

        // HP バー
        GameObject hpBarGroup = CreateChild(battlePanelObj.transform, "HpBarGroup");
        SetRect(hpBarGroup, new Vector2(0, -80), new Vector2(350, 30));
        Slider hpSlider = CreateSlider(hpBarGroup.transform);
        TextMeshProUGUI hpTxt = CreateTMP(battlePanelObj.transform, "HpText", "8 / 8",
            18, TextAlignmentOptions.Center, new Vector2(0, -105));

        // 攻撃ボタン
        Button atkBtn = CreateButton(battlePanelObj.transform, "AttackButton", "攻撃！", new Vector2(0, -160), new Color(0.8f, 0.2f, 0.1f));
        TextMeshProUGUI atkLabel = atkBtn.GetComponentInChildren<TextMeshProUGUI>();

        // ダメージフロートテキスト
        TextMeshProUGUI dmgFloat = CreateTMP(battlePanelObj.transform, "DamageFloat", "-1",
            32, TextAlignmentOptions.Center, new Vector2(60, 80));
        dmgFloat.color = Color.red;

        // 結果
        GameObject bResultBox = CreateResultBox(battlePanelObj.transform, "ResultBox", out TextMeshProUGUI bResultText, out Button bOkBtn);

        SerializedObject battleSO = new SerializedObject(battleScript);
        battleSO.FindProperty("root").objectReferenceValue             = battlePanelObj;
        battleSO.FindProperty("enemyImage").objectReferenceValue       = enemyImg;
        battleSO.FindProperty("enemyNameText").objectReferenceValue    = enemyName;
        battleSO.FindProperty("englishText").objectReferenceValue      = engText;
        battleSO.FindProperty("readingText").objectReferenceValue      = rdText;
        battleSO.FindProperty("hpSlider").objectReferenceValue         = hpSlider;
        battleSO.FindProperty("hpText").objectReferenceValue           = hpTxt;
        battleSO.FindProperty("attackButton").objectReferenceValue     = atkBtn;
        battleSO.FindProperty("attackButtonText").objectReferenceValue = atkLabel;
        battleSO.FindProperty("bossLabel").objectReferenceValue        = bossLbl;
        battleSO.FindProperty("damageFloatText").objectReferenceValue  = dmgFloat;
        battleSO.FindProperty("resultBox").objectReferenceValue        = bResultBox;
        battleSO.FindProperty("resultText").objectReferenceValue       = bResultText;
        battleSO.FindProperty("okButton").objectReferenceValue         = bOkBtn;
        battleSO.ApplyModifiedProperties();

        // ================================================================
        //  5) QuestClearPanel
        // ================================================================
        GameObject clearPanel = CreatePanel(questRoot.transform, "QuestClearPanel", new Color(0.05f, 0.05f, 0.2f, 0.95f));
        QuestClearPanel clearScript = clearPanel.AddComponent<QuestClearPanel>();

        TextMeshProUGUI clearTitle = CreateTMP(clearPanel.transform, "TitleText", "クエストクリア！",
            42, TextAlignmentOptions.Center, new Vector2(0, 180));
        clearTitle.color = Color.yellow;
        TextMeshProUGUI qName = CreateTMP(clearPanel.transform, "QuestNameText", "",
            24, TextAlignmentOptions.Center, new Vector2(0, 130));
        Image lilyImg = CreateImageChild(clearPanel.transform, "LilyImage", null,
            new Vector2(0, 40), new Vector2(160, 200));
        lilyImg.color = new Color(0.9f, 0.8f, 1.0f);
        TextMeshProUGUI clearMsg = CreateTMP(clearPanel.transform, "ClearMessage", "",
            22, TextAlignmentOptions.Center, new Vector2(0, -80));
        TextMeshProUGUI goldStat = CreateTMP(clearPanel.transform, "GoldText", "獲得ゴールド: 0 G",
            22, TextAlignmentOptions.Center, new Vector2(0, -120));
        TextMeshProUGUI expStat = CreateTMP(clearPanel.transform, "ExpText", "EXP: 0",
            22, TextAlignmentOptions.Center, new Vector2(0, -150));
        TextMeshProUGUI itemStat = CreateTMP(clearPanel.transform, "ItemText", "",
            20, TextAlignmentOptions.Center, new Vector2(0, -180));
        Button closeBtn = CreateButton(clearPanel.transform, "CloseButton", "冒険を続ける", new Vector2(0, -230), new Color(0.2f, 0.5f, 0.8f));
        TextMeshProUGUI closeLbl = closeBtn.GetComponentInChildren<TextMeshProUGUI>();

        SerializedObject clearSO = new SerializedObject(clearScript);
        clearSO.FindProperty("root").objectReferenceValue              = clearPanel;
        clearSO.FindProperty("titleText").objectReferenceValue         = clearTitle;
        clearSO.FindProperty("questNameText").objectReferenceValue     = qName;
        clearSO.FindProperty("lilyImage").objectReferenceValue         = lilyImg;
        clearSO.FindProperty("clearMessageText").objectReferenceValue  = clearMsg;
        clearSO.FindProperty("goldText").objectReferenceValue          = goldStat;
        clearSO.FindProperty("expText").objectReferenceValue           = expStat;
        clearSO.FindProperty("itemText").objectReferenceValue          = itemStat;
        clearSO.FindProperty("closeButton").objectReferenceValue       = closeBtn;
        clearSO.FindProperty("closeButtonLabel").objectReferenceValue  = closeLbl;
        clearSO.ApplyModifiedProperties();

        // ================================================================
        //  6) PipHintController
        // ================================================================
        GameObject pipObj = CreateChild(questRoot.transform, "PipHint");
        SetRect(pipObj, new Vector2(-170, -250), new Vector2(260, 120));
        PipHintController pipScript = pipObj.AddComponent<PipHintController>();

        Image pipImg = CreateImageChild(pipObj.transform, "PipImage", null,
            new Vector2(-80, 0), new Vector2(70, 70));
        pipImg.color = new Color(0.4f, 0.8f, 0.4f);

        GameObject bubble = CreateChild(pipObj.transform, "SpeechBubble");
        SetRect(bubble, new Vector2(40, 0), new Vector2(180, 100));
        Image bubbleBg = bubble.AddComponent<Image>();
        bubbleBg.color = new Color(1f, 1f, 1f, 0.9f);
        CanvasGroup bubbleCG = bubble.AddComponent<CanvasGroup>();
        TextMeshProUGUI hintTxt = CreateTMP(bubble.transform, "HintText", "",
            16, TextAlignmentOptions.Left, Vector2.zero);
        hintTxt.color = Color.black;
        SetSize(hintTxt, 160, 80);

        SerializedObject pipSO = new SerializedObject(pipScript);
        pipSO.FindProperty("root").objectReferenceValue          = pipObj;
        pipSO.FindProperty("pipImage").objectReferenceValue      = pipImg;
        pipSO.FindProperty("speechBubble").objectReferenceValue  = bubble;
        pipSO.FindProperty("hintText").objectReferenceValue      = hintTxt;
        pipSO.ApplyModifiedProperties();

        // ================================================================
        //  QuestManager に全パネルをアタッチ
        // ================================================================
        SerializedObject qmSO = new SerializedObject(qm);
        qmSO.FindProperty("choicePanel").objectReferenceValue   = choiceScript;
        qmSO.FindProperty("villagerPanel").objectReferenceValue = villagerScript;
        qmSO.FindProperty("chestPanel").objectReferenceValue    = chestScript;
        qmSO.FindProperty("battlePanel").objectReferenceValue   = battleScript;
        qmSO.FindProperty("clearPanel").objectReferenceValue    = clearScript;
        qmSO.FindProperty("pipHint").objectReferenceValue       = pipScript;
        qmSO.ApplyModifiedProperties();

        // ================================================================
        //  テスト開始ボタン (開発用)
        //  ※ AddListener は Editor では保存されないので
        //    QuestStartButton コンポーネントを使う
        // ================================================================
        Button startBtn = CreateButton(questRoot.transform, "DEBUG_StartQuestButton", "クエスト開始 (テスト)",
            new Vector2(0, 280), new Color(0.1f, 0.7f, 0.1f));
        startBtn.gameObject.AddComponent<QuestStartButton>();

        // 選択して完了
        Selection.activeGameObject = managerObj;
        Debug.Log("<color=green>★ Quest UI セットアップ完了！Hierarchyを確認してください。</color>");
        Debug.Log("「DEBUG_StartQuestButton」をクリックするか、QuestManager.Instance.StartQuest() で開始できます。");

        return new Result
        {
            managerObj  = managerObj,
            questRoot   = questRoot,
            startButton = startBtn,
        };
    }

    static void ConfigureGameStartButton(Button startButton)
    {
        if (startButton == null)
        {
            return;
        }

        startButton.gameObject.name = "QuestStartButton";

        RectTransform rt = startButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-32f, -150f);
        rt.sizeDelta = new Vector2(180f, 56f);

        TextMeshProUGUI label = startButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = "クエスト";
            label.fontSize = 24f;
        }
    }

    // ================================================================
    //  ユーティリティ関数
    // ================================================================

    static GameObject CreateChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    static GameObject CreatePanel(Transform parent, string name, Color bgColor)
    {
        GameObject panel = CreateChild(parent, name);
        StretchFull(panel.GetComponent<RectTransform>());
        Image bg = panel.AddComponent<Image>();
        bg.color = bgColor;
        return panel;
    }

    static Image CreateImageChild(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateChild(parent, name);
        SetRect(obj, pos, size);
        Image img = obj.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        return img;
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string name, string text,
                                     float fontSize, TextAlignmentOptions align, Vector2 pos)
    {
        GameObject obj = CreateChild(parent, name);
        SetRect(obj, pos, new Vector2(400, 50));
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color color)
    {
        GameObject obj = CreateChild(parent, name);
        SetRect(obj, pos, new Vector2(240, 60));
        Image bg = obj.AddComponent<Image>();
        bg.color = color;
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = bg;

        TextMeshProUGUI txt = CreateTMP(obj.transform, "Label", label,
            22, TextAlignmentOptions.Center, Vector2.zero);
        StretchFull(txt.GetComponent<RectTransform>());
        return btn;
    }

    static Slider CreateSlider(Transform parent)
    {
        GameObject obj = CreateChild(parent.transform, "HpSlider");
        StretchFull(obj.GetComponent<RectTransform>());
        Slider slider = obj.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;

        // Background
        GameObject bg = CreateChild(obj.transform, "Background");
        StretchFull(bg.GetComponent<RectTransform>());
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f);

        // Fill Area
        GameObject fillArea = CreateChild(obj.transform, "Fill Area");
        StretchFull(fillArea.GetComponent<RectTransform>());
        GameObject fill = CreateChild(fillArea.transform, "Fill");
        StretchFull(fill.GetComponent<RectTransform>());
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.2f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        return slider;
    }

    static GameObject CreateResultBox(Transform parent, string name,
                                      out TextMeshProUGUI resultText, out Button okButton)
    {
        GameObject box = CreateChild(parent, name);
        SetRect(box, Vector2.zero, new Vector2(420, 200));
        Image boxBg = box.AddComponent<Image>();
        boxBg.color = new Color(0f, 0f, 0f, 0.85f);

        resultText = CreateTMP(box.transform, "ResultText", "",
            22, TextAlignmentOptions.Center, new Vector2(0, 20));
        SetSize(resultText, 380, 100);

        okButton = CreateButton(box.transform, "OKButton", "OK", new Vector2(0, -60), new Color(0.3f, 0.6f, 0.3f));

        return box;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetRect(GameObject obj, Vector2 pos, Vector2 size)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetSize(TextMeshProUGUI tmp, float w, float h)
    {
        RectTransform rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
    }
}
