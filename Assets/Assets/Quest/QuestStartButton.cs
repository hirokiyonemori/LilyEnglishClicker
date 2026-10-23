using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ボタンに付けるだけでクエストを開始できるヘルパー
/// </summary>
[RequireComponent(typeof(Button))]
public class QuestStartButton : MonoBehaviour
{
    [Header("使うクエストデータ（空ならデフォルト）")]
    public QuestDataSO questData;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (!QuestEntryController.IsUnlocked) return;
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.StartQuest(questData);
            Debug.Log("★ クエスト開始ボタンが押されました！");
        }
        else
        {
            Debug.LogError("QuestManager.Instance が見つかりません！シーンに QuestManager がありますか？");
        }
    }
}
