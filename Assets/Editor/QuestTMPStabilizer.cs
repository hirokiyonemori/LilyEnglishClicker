using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class QuestTMPStabilizer
{
    public const string QuestFontPath = "Assets/Font/NotoSansJP-Bold SDF.asset";

    [MenuItem("LilyClicker/Stabilize Quest TMP")]
    public static void StabilizeCurrentScene()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(QuestFontPath);
        if (font == null)
        {
            Debug.LogError($"[QuestTMPStabilizer] Font not found: {QuestFontPath}");
            return;
        }

        GameObject[] roots = Object.FindObjectsOfType<GameObject>(true)
            .Where(go => go.name == "QuestUIRoot")
            .ToArray();

        if (roots.Length == 0)
        {
            Debug.LogWarning("[QuestTMPStabilizer] QuestUIRoot was not found in the current scene.");
            return;
        }

        int tmpCount = 0;
        int legacyTextCount = 0;

        foreach (GameObject root in roots)
        {
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Undo.RecordObject(text, "Stabilize Quest TMP");
                text.font = font;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
                tmpCount++;
            }

            legacyTextCount += root.GetComponentsInChildren<Text>(true).Length;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[QuestTMPStabilizer] Applied {font.name} to {tmpCount} TMP texts under QuestUIRoot. Legacy Text under QuestUIRoot: {legacyTextCount}");
    }
}
