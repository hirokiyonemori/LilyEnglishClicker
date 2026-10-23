using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>既存メニューの互換入口。生成内容は製品版Quest UIに統一する。</summary>
public class QuestSceneSetup : EditorWindow
{
    public class Result
    {
        public GameObject managerObj;
        public GameObject questRoot;
        public Button startButton;
    }

    [MenuItem("LilyClicker/Quest UI を自動生成")]
    private static void CreateQuestUI() { Build(); }

    [MenuItem("LilyClicker/GameシーンにQuestオーバーレイを組み込む")]
    private static void InstallIntoGameScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        Build();
        EditorSceneManager.SaveScene(scene);
    }

    public static Result Build()
    {
        QuestUIView view = QuestProductionUIBuilder.InstallCurrentScene();
        QuestManager manager = null;
        foreach (QuestManager candidate in Object.FindObjectsByType<QuestManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.gameObject.scene == SceneManager.GetActiveScene()) { manager = candidate; break; }
        return new Result
        {
            managerObj = manager != null ? manager.gameObject : null,
            questRoot = view.questRoot,
            startButton = view.openButton
        };
    }
}
