using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// クリッカー本編の画面からクエストを重ねて開始・終了するためのコントローラ。
///
/// クエストは独立シーンではなくGameシーン内のオーバーレイとして動かす。
/// シーンを分けると Managers / DataManager / UpgradeManager 一式を
/// 二重に持つことになり、所持金などの状態も持ち越せないため。
/// </summary>
public class QuestOverlayController : MonoBehaviour
{
    [Header("クエストUIのルート（非アクティブで配置しておく）")]
    public GameObject questRoot;

    [Header("クエストを開始するボタン（本編画面側に置く）")]
    public Button openButton;

    [Header("クエスト中に隠す本編UI（任意）")]
    public GameObject[] hideWhileQuesting;

    private bool subscribed;

    private void Start()
    {
        if (questRoot != null)
        {
            // 背景の出し分け（画像が無ければ何もしない）。Prefab を触らずに済むようコードから付ける
            if (questRoot.GetComponent<QuestBackgroundSwitcher>() == null)
            {
                questRoot.AddComponent<QuestBackgroundSwitcher>();
            }

            questRoot.SetActive(false);
        }

        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenQuest);
        }

        TrySubscribe();
    }

    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenQuest);
        }

        if (subscribed && QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestFinished -= CloseQuest;
        }
    }

    /// <summary>
    /// QuestManager は Awake で Instance を設定するが、
    /// 実行順によっては Start 時点でまだ null のことがあるため遅延購読する
    /// </summary>
    private void TrySubscribe()
    {
        if (subscribed || QuestManager.Instance == null) return;

        QuestManager.Instance.OnQuestFinished += CloseQuest;
        subscribed = true;
    }

    public void OpenQuest()
    {
        if (!QuestEntryController.IsUnlocked) return;
        TrySubscribe();

        if (QuestManager.Instance == null)
        {
            Debug.LogError("QuestOverlayController: QuestManager がシーンにありません。");
            return;
        }

        if (QuestManager.IsQuestActive)
        {
            Debug.LogWarning("QuestOverlayController: すでにクエスト進行中です。");
            return;
        }

        SetHiddenUIActive(false);

        if (questRoot != null)
        {
            questRoot.SetActive(true);
        }

        QuestManager.Instance.StartQuest();
    }

    public void CloseQuest()
    {
        if (questRoot != null)
        {
            questRoot.SetActive(false);
        }

        SetHiddenUIActive(true);
    }

    private void SetHiddenUIActive(bool active)
    {
        if (hideWhileQuesting == null) return;

        foreach (GameObject go in hideWhileQuesting)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
