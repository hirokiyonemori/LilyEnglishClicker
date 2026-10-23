using Idle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>手動で開く任意のデイリー。閉じる・不正解で本編を止めない。</summary>
public sealed class DailyEnglishBonusPanel : MonoBehaviour
{
    public Button openButton, closeButton, claimButton, answerA, answerB;
    public GameObject panelRoot;
    public TMP_Text status, wordText, readingText, answerAText, answerBText, claimText;
    private DailyBonusService service;
    private DailyWordQuestion question;
    private QuestEntryController questEntry;

    private bool OtherPanelOpen => QuestManager.IsQuestActive
        || (LilyEnglish.Growth.UI.GrowthUIView.Find() != null && LilyEnglish.Growth.UI.GrowthUIView.Find().IsOpen)
        || (questEntry != null && questEntry.IsDialogOpen);

    private void Awake()
    {
        // Canvas Prefabの編集時に保存されたゼロスケールを初期化する。
        transform.localScale = Vector3.one;
        panelRoot.SetActive(false);
        questEntry = FindAnyObjectByType<QuestEntryController>(FindObjectsInactive.Include);
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        claimButton.onClick.AddListener(Claim);
        answerA.onClick.AddListener(()=>Answer(0));
        answerB.onClick.AddListener(()=>Answer(1));
    }
    private void Update()
    {
        openButton.gameObject.SetActive(!OtherPanelOpen);
        if (OtherPanelOpen || (panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))) Close();
        if (panelRoot.activeSelf && service != null && question.day != service.Today) Refresh();
    }
    public void Open()
    {
        if (DataManager.data == null || OtherPanelOpen) return;
        service = new DailyBonusService(DataManager.data, DataManager.SaveData);
        panelRoot.SetActive(true);
        Refresh();
    }
    public void Close() { if (panelRoot != null) panelRoot.SetActive(false); }
    private void Claim()
    {
        if (service == null) return;
        service.ClaimBase(out string message);
        Refresh(); status.text = message; NotifyWallet();
    }
    private void Answer(int index)
    {
        if (service == null || question == null) return;
        service.Answer(index, question.day, out string message);
        Refresh(); status.text = message; NotifyWallet();
    }
    private void NotifyWallet()
    {
        var growth = LilyEnglish.Growth.GrowthService.Instance;
        if (growth != null) growth.NotifyWalletChanged();
    }
    private void Refresh()
    {
        question = service.Question();
        wordText.text = question.word.english;
        readingText.text = question.word.reading;
        answerAText.text = question.answerA; answerBText.text = question.answerB;
        claimButton.interactable = service.CanClaimBase;
        claimText.text = service.CanClaimBase ? "通常100 GOLDを受け取る" : "通常報酬：受け取り済み";
        answerA.interactable = answerB.interactable = service.CanAnswer;
        status.text = service.CanAnswer ? "英語は任意です。正解すると追加50 GOLD！" :
            (service.AnsweredCorrectly ? "今日の追加50 GOLDは受け取り済みです" : "今日の英語ボーナスは終了しました");
    }
}
