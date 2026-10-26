using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>既存アートと参照を保ったまま戦闘入力を変更し、任意のボーナス入口を追加する。</summary>
public static class GameplayTempoMigration
{
    public const string DailyPrefab = "Assets/Assets/Prefabs/DailyEnglishBonus.prefab";
    static TMP_FontAsset font;
    static readonly Color Ink = new Color(.055f,.10f,.16f,1), Gold = new Color(.85f,.73f,.48f,1);
    [MenuItem("LilyClicker/ゲームテンポ/通常戦闘・デイリーボーナスを組み込む")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Assets/Prefabs/QuestUIRoot.prefab");
        PatchBattles(prefab);
        PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Assets/Prefabs/QuestUIRoot.prefab");
        PrefabUtility.UnloadPrefabContents(prefab);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NotoSansJP-Bold SDF.asset");
        var daily = BuildDaily();
        PrefabUtility.SaveAsPrefabAsset(daily, DailyPrefab);
        Object.DestroyImmediate(daily);
        foreach (var path in new[]{"Assets/Scenes/Game.unity", "Assets/Scenes/Quest.unity"})
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects()) PatchBattles(root);
            if (!Object.FindObjectsByType<DailyEnglishBonusPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any())
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DailyPrefab), scene);
            EditorSceneManager.SaveScene(scene);
        }
        var skill = AssetDatabase.LoadAssetAtPath<LilyEnglish.Growth.SkillDefinition>("Assets/Data/Growth/Skill_SpellStar.asset");
        if (skill != null) {
            skill.descriptionFormat = "冒険の攻撃で、攻撃力の{0}%のダメージを与える";
            skill.requireCorrectAnswer = false;
            EditorUtility.SetDirty(skill);
        }
        AssetDatabase.SaveAssets();
        if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("[TempoMigration] Existing battle assets updated; daily bonus installed.");
    }
    static void PatchBattles(GameObject root)
    {
        foreach (var battle in root.GetComponentsInChildren<BattlePanel>(true))
        {
            if (battle.HasCommandMenu) continue;
            var so = new SerializedObject(battle);
            so.FindProperty("turnDelay").floatValue = .22f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var button = (Button)so.FindProperty("attackButton").objectReferenceValue;
            if (button != null) {
                var rect = (RectTransform)button.transform;
                rect.anchorMin=new Vector2(.12f,.34f); rect.anchorMax=new Vector2(.88f,.46f);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                var group=button.GetComponent<CanvasGroup>();
                if(group!=null) { group.alpha=1;group.interactable=true;group.blocksRaycasts=true; }
                button.gameObject.SetActive(true);
                var label=button.GetComponentInChildren<TMP_Text>(true);
                if(label!=null) { label.text="攻撃！";label.fontSize=44; }
            }
            foreach(var t in battle.GetComponentsInChildren<Transform>(true)) {
                if(t.name=="WordQuestionCard" || t.name=="AnswerFeedback") t.gameObject.SetActive(false);
                var label=t.GetComponent<TMP_Text>();
                if(label!=null && t.parent!=null && t.parent.name=="ChapterHeader") {
                    if(t.name=="Chapter") label.text="BATTLE";
                    if(t.name=="Subtitle") label.text="リリィと一緒に戦おう";
                }
            }
            // Prefab instance overrides must also persist for scene-local battle panels.
            foreach(var c in battle.GetComponentsInChildren<Component>(true))
                if(c!=null && PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }
    static GameObject BuildDaily()
    {
        var go=new GameObject("DailyEnglishBonus",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        var panel=go.AddComponent<DailyEnglishBonusPanel>();
        var safe=Rect(go.transform,"SafeArea",0,0,1,1);
        safe.gameObject.AddComponent<QuestSafeArea>();
        panel.openButton=Button(safe,"DailyEntry","今日のボーナス",.64f,.87f,.96f,.92f,27);
        var dim=Rect(go.transform,"DailyModal",0,0,1,1);
        var veil=dim.gameObject.AddComponent<Image>();veil.color=new Color(0,0,0,.85f);
        panel.panelRoot=dim.gameObject;
        var modalSafe=Rect(dim,"SafeArea",0,0,1,1);modalSafe.gameObject.AddComponent<QuestSafeArea>();
        var card=Rect(modalSafe,"Card",.065f,.13f,.935f,.88f);
        var face=card.gameObject.AddComponent<QuestSurfaceGraphic>();face.topColor=Ink;face.bottomColor=new Color(.09f,.17f,.24f);face.borderColor=Gold;face.borderWidth=3;face.cornerRadius=24;face.raycastTarget=true;
        Label(card,"Title","デイリーボーナス",.06f,.89f,.94f,.97f,45);
        Label(card,"Intro","毎日100 GOLD。英語の挑戦はお好みで。",.05f,.82f,.95f,.88f,27);
        panel.claimButton=Button(card,"Claim","通常100 GOLDを受け取る",.08f,.71f,.92f,.80f,34);
        panel.claimText=panel.claimButton.GetComponentInChildren<TMP_Text>();
        Label(card,"Eyebrow","TODAY'S WORD",.08f,.63f,.92f,.68f,27);
        panel.wordText=Label(card,"Word","hero",.08f,.53f,.92f,.63f,66);
        panel.readingText=Label(card,"Reading","ヒーロー",.08f,.48f,.92f,.53f,27);
        Label(card,"Question","この英単語の意味は？",.08f,.41f,.92f,.47f,30);
        panel.answerA=Button(card,"AnswerA","水",.08f,.30f,.48f,.39f,34);
        panel.answerB=Button(card,"AnswerB","英雄",.52f,.30f,.92f,.39f,34);
        panel.answerAText=panel.answerA.GetComponentInChildren<TMP_Text>();panel.answerBText=panel.answerB.GetComponentInChildren<TMP_Text>();
        panel.status=Label(card,"Status","英語は任意です。正解すると追加50 GOLD！",.06f,.17f,.94f,.28f,28);
        panel.closeButton=Button(card,"Close","閉じてゲームに戻る",.15f,.045f,.85f,.135f,32);
        dim.gameObject.SetActive(false);
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
        var rootRect = (RectTransform)go.transform;
        rootRect.localScale = Vector3.one; rootRect.sizeDelta = new Vector2(1080,1920); rootRect.pivot = Vector2.one * .5f;
        return go;
    }
    static RectTransform Rect(Transform parent,string name,float x0,float y0,float x1,float y1) {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static TMP_Text Label(Transform p,string name,string text,float x0,float y0,float x1,float y1,float size) {
        var t=Rect(p,name,x0,y0,x1,y1).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;
        t.alignment=TextAlignmentOptions.Center;t.color=new Color(.96f,.93f,.85f);t.raycastTarget=false;return t;
    }
    static Button Button(Transform p,string name,string text,float x0,float y0,float x1,float y1,float size) {
        var r=Rect(p,name,x0,y0,x1,y1);var g=r.gameObject.AddComponent<QuestSurfaceGraphic>();
        g.topColor=new Color(.18f,.31f,.4f);g.bottomColor=new Color(.11f,.21f,.3f);g.borderColor=Gold;g.cornerRadius=14;g.borderWidth=2;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=g;var colors=b.colors;colors.disabledColor=new Color(.5f,.5f,.5f,.7f);b.colors=colors;
        Label(r,"Label",text,.03f,.04f,.97f,.96f,size);return b;
    }
}
