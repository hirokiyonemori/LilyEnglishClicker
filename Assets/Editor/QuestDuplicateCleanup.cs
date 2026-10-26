using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QuestDuplicateCleanup
{
    [MenuItem("LilyClicker/Cleanup Duplicate Quest UI")]
    public static void CleanupCurrentScene()
    {
        QuestManager[] managers = Object.FindObjectsOfType<QuestManager>(true);
        GameObject[] roots = Object.FindObjectsOfType<GameObject>(true)
            .Where(go => go.name == "QuestUIRoot")
            .ToArray();

        QuestManager keeper = managers
            .OrderByDescending(GetReferenceScore)
            .ThenByDescending(m => GetTransformOrder(m.transform))
            .FirstOrDefault();

        GameObject keepRoot = FindReferencedRoot(keeper) ?? roots
            .OrderByDescending(GetDescendantCount)
            .ThenByDescending(go => GetTransformOrder(go.transform))
            .FirstOrDefault();

        int deletedManagers = 0;
        foreach (QuestManager manager in managers)
        {
            if (manager == keeper)
            {
                continue;
            }

            Object.DestroyImmediate(manager.gameObject);
            deletedManagers++;
        }

        int deletedRoots = 0;
        foreach (GameObject root in roots)
        {
            if (root == keepRoot)
            {
                continue;
            }

            Object.DestroyImmediate(root);
            deletedRoots++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[QuestDuplicateCleanup] Complete. Kept manager: {(keeper != null ? keeper.name : "none")}, kept root: {(keepRoot != null ? keepRoot.name : "none")}, deleted managers: {deletedManagers}, deleted roots: {deletedRoots}");
    }

    private static int GetReferenceScore(QuestManager manager)
    {
        if (manager == null)
        {
            return -1;
        }

        int score = 0;
        if (manager.choicePanel != null) score++;
        if (manager.villagerPanel != null) score++;
        if (manager.chestPanel != null) score++;
        if (manager.battlePanel != null) score++;
        if (manager.clearPanel != null) score++;
        if (manager.pipHint != null) score++;
        return score;
    }

    private static GameObject FindReferencedRoot(QuestManager manager)
    {
        if (manager == null || manager.choicePanel == null)
        {
            return null;
        }

        Transform current = manager.choicePanel.transform;
        while (current != null)
        {
            if (current.name == "QuestUIRoot")
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static int GetDescendantCount(GameObject gameObject)
    {
        return gameObject == null ? -1 : gameObject.GetComponentsInChildren<Transform>(true).Length;
    }

    private static int GetTransformOrder(Transform transform)
    {
        if (transform == null)
        {
            return -1;
        }

        int order = transform.GetSiblingIndex();
        Transform current = transform.parent;
        while (current != null)
        {
            order += current.GetSiblingIndex() * 1000;
            current = current.parent;
        }

        return order;
    }
}
