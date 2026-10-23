using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DialogueHeaderLayout
{
    public static void Apply(RectTransform bar)
    {
        Set(bar, .065f, .935f, 24, 200);
        var mark = (RectTransform)bar.Find("Mark");
        Set(mark, .5f, .5f, 16, 72);
        mark.sizeDelta = new Vector2(72, 72);
        Set((RectTransform)bar.Find("Chapter"), .06f, .94f, 98, 34);
        Set((RectTransform)bar.Find("Subtitle"), .06f, .94f, 140, 42);
        foreach (string name in new[] { "Chapter", "Subtitle" })
        {
            var text = bar.Find(name).GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = name == "Chapter" ? 25 : 31;
            text.fontSizeMin = name == "Chapter" ? 18 : 24;
        }
    }
    static void Set(RectTransform r, float left, float right, float top, float height)
    {
        r.anchorMin = new Vector2(left, 1); r.anchorMax = new Vector2(right, 1);
        r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(0, -top);
        r.sizeDelta = new Vector2(0, height);
    }
    static void ApplyRoot(GameObject root)
    {
        foreach (var panel in root.GetComponentsInChildren<VillagerEventPanel>(true))
        {
            var bar = panel.transform.Find("Content/ChapterHeader") as RectTransform;
            if (bar != null) Apply(bar);
        }
    }
    [MenuItem("LilyClicker/Dialogue Header/Apply Layout")]
    public static void Migrate()
    {
        const string path = "Assets/Assets/Prefabs/QuestUIRoot.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try { ApplyRoot(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects()) ApplyRoot(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DialogueHeader] Scene and prefab updated");
    }
}
