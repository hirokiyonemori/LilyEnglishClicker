using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>本編とは別の、セーブを読み書きしない実機比較シーンを用意する。</summary>
public static class WordMagicPlaytestBuilder
{
    public const string ScenePath = "Assets/Scenes/WordMagicPlaytest.unity";
    [MenuItem("LilyClicker/ことば魔法/3連戦テストを開く")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Playを停止してから開いてください。"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
        EditorSceneManager.OpenScene(ScenePath);
    }

    // バッチ検証でも呼べる。現在のシーンを閉じずに新規シーンだけ保存する。
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/Prefabs/QuestUIRoot.prefab");
        if (prefab == null) throw new System.InvalidOperationException("QuestUIRoot prefab is missing.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            var camera = new GameObject("Camera",typeof(Camera),typeof(AudioListener));
            camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor=new Color(.035f,.075f,.13f);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var runner = new GameObject("WordMagicPlaytest").AddComponent<WordMagicPlaytest>();
            runner.questPrefab=prefab.GetComponent<QuestUIView>();
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene,true);
            if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
