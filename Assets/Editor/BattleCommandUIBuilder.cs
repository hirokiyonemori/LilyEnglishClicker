using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>既存の戦闘だけを更新する。本編・報酬・セーブ・敵データは編集しない。</summary>
public static class BattleCommandUIBuilder
{
    const string PrefabPath = "Assets/Assets/Prefabs/QuestUIRoot.prefab";
    static TMP_FontAsset font;
    static readonly Color Gold = new Color(.85f,.73f,.48f);
    [MenuItem("LilyClicker/冒険/攻撃・魔法・防御UIを組み込む")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        foreach(var battle in root.GetComponentsInChildren<BattlePanel>(true)) Install(battle);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        foreach(var path in new[]{"Assets/Scenes/Game.unity","Assets/Scenes/Quest.unity"}) {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var go in scene.GetRootGameObjects())
                foreach(var battle in go.GetComponentsInChildren<BattlePanel>(true)) Install(battle);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if(!Application.isBatchMode)EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("[BattleCommands] Prefab and Game/Quest scene commands installed.");
    }
    public static void Install(BattlePanel battle)
    {
        var battleObject = new SerializedObject(battle);
        var battleRoot = (GameObject)battleObject.FindProperty("root").objectReferenceValue;
        var existing = battle.GetComponent<BattleCommandMenu>();
        if(existing != null) {
            existing.commandRoot.transform.SetParent(battleRoot.transform, false);
            existing.commandRoot.SetActive(false);
            EditorUtility.SetDirty(existing);
            return;
        }
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NotoSansJP-Bold SDF.asset");
        var so=new SerializedObject(battle);
        var attack=(Button)so.FindProperty("attackButton").objectReferenceValue;
        if(attack==null)throw new InvalidOperationException("Battle attack reference missing");
        var menu=battle.gameObject.AddComponent<BattleCommandMenu>();
        Set(battle,"commandMenu",menu);
        var root=Rect(battleRoot.transform,"BattleCommands",.10f,.29f,.90f,.54f);
        Surface(root);
        menu.commandRoot=root.gameObject;
        menu.messageText=Label(root,"ActionMessage","行動を選んでください",.035f,.82f,.965f,.98f,28);
        var main=Rect(root,"MainCommands",0,0,1,.81f);menu.mainCommands=main.gameObject;
        var spells=Rect(root,"SpellCommands",0,0,1,.81f);menu.spellCommands=spells.gameObject;
        attack.transform.SetParent(main,false);
        Place((RectTransform)attack.transform,.06f,.51f,.94f,.96f);
        var group=attack.GetComponent<CanvasGroup>();if(group!=null){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
        var attackText=attack.GetComponentInChildren<TMP_Text>(true);attackText.text="攻撃";attackText.fontSize=42;
        menu.magicButton=Button(main,"MagicCommand","魔法",.06f,.06f,.48f,.45f);
        menu.defendButton=Button(main,"DefendCommand","防御",.52f,.06f,.94f,.45f);
        menu.fireButton=Button(spells,"FireSpell","ファイア　MP 2",.06f,.51f,.94f,.96f);
        menu.fireText=menu.fireButton.GetComponentInChildren<TMP_Text>();
        menu.backButton=Button(spells,"BackCommand","← 戻る",.06f,.06f,.94f,.45f);
        var presentation=battle.GetComponent<QuestBattlePresentation>();
        var ps=new SerializedObject(presentation);
        var hpText=(TMP_Text)ps.FindProperty("lilyHpText").objectReferenceValue;
        var player=(RectTransform)hpText.transform.parent;
        Place(player,.06f,.08f,.94f,.26f);
        Place((RectTransform)player.Find("LilyName"),.34f,.75f,.94f,.96f);
        Place((RectTransform)player.Find("TurnText"),.34f,.58f,.94f,.75f);
        Place(hpText.rectTransform,.34f,.40f,.94f,.57f);
        Place((RectTransform)player.Find("LilyHpBar"),.34f,.34f,.93f,.39f);
        var mp=Rect(player,"LilyMp",.34f,.06f,.94f,.33f);menu.mpRoot=mp.gameObject;
        menu.mpText=Label(mp,"MpText","MP 5 / 5",0,.33f,1,1,29);menu.mpText.alignment=TextAlignmentOptions.Left;
        var bar=Rect(mp,"MpBar",0,.02f,.98f,.23f);var bg=bar.gameObject.AddComponent<Image>();bg.color=new Color(.06f,.12f,.20f);
        var fill=Rect(bar,"Fill",0,0,1,1);var face=fill.gameObject.AddComponent<Image>();face.color=new Color(.35f,.58f,1f);face.raycastTarget=false;
        var slider=bar.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.minValue=0;slider.maxValue=5;slider.value=5;slider.interactable=false;
        menu.mpSlider=slider;bg.raycastTarget=false;
        // 終端状態ではBattleCommandMenu.Refreshがコマンド部分を非表示にする。
        spells.gameObject.SetActive(false);
        root.gameObject.SetActive(false);
        EditorUtility.SetDirty(battle);EditorUtility.SetDirty(menu);
    }
    static void Set(Object o,string name,Object value){var s=new SerializedObject(o);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static RectTransform Rect(Transform parent,string name,float x0,float y0,float x1,float y1){
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;r.SetParent(parent,false);Place(r,x0,y0,x1,y1);return r;
    }
    static void Place(RectTransform r,float x0,float y0,float x1,float y1){r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;}
    static QuestSurfaceGraphic Surface(RectTransform r){var g=r.gameObject.AddComponent<QuestSurfaceGraphic>();g.topColor=new Color(.12f,.22f,.32f);g.bottomColor=new Color(.07f,.13f,.23f);g.borderColor=Gold;g.borderWidth=2;g.cornerRadius=24;return g;}
    static TMP_Text Label(Transform p,string name,string text,float x0,float y0,float x1,float y1,float size){var t=Rect(p,name,x0,y0,x1,y1).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.96f,.93f,.85f);t.raycastTarget=false;return t;}
    static Button Button(Transform p,string name,string text,float x0,float y0,float x1,float y1){
        var r=Rect(p,name,x0,y0,x1,y1);var g=Surface(r);g.topColor=new Color(.20f,.34f,.44f);g.cornerRadius=16;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=g;var c=b.colors;c.disabledColor=new Color(.38f,.38f,.38f,1);b.colors=c;
        Label(r,"Label",text,.03f,.04f,.97f,.96f,40);return b;
    }
}
