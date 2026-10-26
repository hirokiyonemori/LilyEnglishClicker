using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 村人イベントパネル
/// ドラクエ風テキスト送り → 選択肢 → 結果
/// </summary>
public class VillagerEventPanel : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image       villagerImage;
    [SerializeField] private Sprite      defaultVillagerSprite;

    [Tooltip("話者名ごとの立ち絵。組み込みシナリオ（コード定義）にも画像を当てられる。VillagerScenario.portrait があればそちらが優先")]
    [SerializeField] private SpeakerPortrait[] speakerPortraits;

    [Serializable]
    private class SpeakerPortrait
    {
        public string speakerName;
        public Sprite sprite;
    }

    [Header("ダイアログ")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject      tapPrompt;    // ▼タップで次へ
    [SerializeField] private Button          advanceButton;

    [Header("選択肢")]
    [SerializeField] private GameObject      choiceGroup;
    [SerializeField] private Button          choiceButtonA;
    [SerializeField] private TextMeshProUGUI choiceLabelA;
    [SerializeField] private Button          choiceButtonB;
    [SerializeField] private TextMeshProUGUI choiceLabelB;

    [Header("結果")]
    [SerializeField] private GameObject      resultBox;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button          okButton;

    [Header("テキスト演出")]
    [SerializeField] private float charInterval = 0.04f;

    private VillagerScenario         currentScenario;
    private int                      lineIndex;
    private Action<QuestEventResult> onComplete;
    private bool advanceRequested;

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(VillagerScenario scenario, Action<QuestEventResult> callback)
    {
        currentScenario = scenario;
        onComplete      = callback;
        lineIndex       = 0;
        advanceRequested = false;
        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(OnAdvanceClicked);
            advanceButton.onClick.AddListener(OnAdvanceClicked);
            advanceButton.interactable = false;
        }

        // 前の人物の画像が残らないよう、画像が無い時も必ず代入し直す（null なら代替エンブレムに戻る）
        if (villagerImage != null) villagerImage.sprite = ResolvePortrait(scenario);

        if (speakerNameText != null) speakerNameText.text = scenario.speakerName;

        if (choiceGroup != null) choiceGroup.SetActive(false);
        if (resultBox != null) resultBox.SetActive(false);
        if (tapPrompt != null) tapPrompt.SetActive(false);

        if (root != null) root.SetActive(true);
        StartCoroutine(RunDialogue());
    }

    /// <summary>立ち絵の優先順位: シナリオ個別 → 話者名の対応表 → 既定の画像 → なし（代替エンブレム）</summary>
    private Sprite ResolvePortrait(VillagerScenario scenario)
    {
        if (scenario != null)
        {
            if (scenario.portrait != null) return scenario.portrait;

            if (speakerPortraits != null)
            {
                foreach (SpeakerPortrait entry in speakerPortraits)
                {
                    if (entry != null && entry.sprite != null && entry.speakerName == scenario.speakerName)
                        return entry.sprite;
                }
            }
        }

        return defaultVillagerSprite;
    }

    public void Hide()
    {
        StopAllCoroutines();
        advanceRequested = false;
        if (advanceButton != null)
        {
            advanceButton.interactable = false;
            advanceButton.onClick.RemoveListener(OnAdvanceClicked);
        }
        if (root != null) root.SetActive(false);
    }

    // ─── ダイアログ進行 ──────────────────────────────────────────
    private IEnumerator RunDialogue()
    {
        VillagerLine[] lines = currentScenario != null ? currentScenario.lines : null;
        while (lines != null && lineIndex < lines.Length)
        {
            yield return StartCoroutine(TypeLine(lines[lineIndex] != null ? lines[lineIndex].text : ""));
            if (tapPrompt != null) tapPrompt.SetActive(true);
            yield return WaitForTap();
            if (tapPrompt != null) tapPrompt.SetActive(false);
            lineIndex++;
        }
        ShowChoices();
    }

    private IEnumerator TypeLine(string line)
    {
        if (dialogueText == null) yield break;
        dialogueText.text = "";
        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(charInterval);
        }
    }

    private IEnumerator WaitForTap()
    {
        advanceRequested = false;
        if (advanceButton != null) advanceButton.interactable = true;
        yield return new WaitUntil(() =>
            advanceRequested ||
            Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began));
        advanceRequested = false;
        if (advanceButton != null) advanceButton.interactable = false;
        yield return null;
    }

    private void OnAdvanceClicked()
    {
        if (isActiveAndEnabled && advanceButton != null && advanceButton.interactable)
            advanceRequested = true;
    }

    // ─── 選択肢 ──────────────────────────────────────────────────
    private void ShowChoices()
    {
        advanceRequested = false;
        if (advanceButton != null) advanceButton.interactable = false;
        if (choiceButtonA != null)
        {
            if (choiceLabelA != null) choiceLabelA.text = currentScenario.choiceALabel;
            choiceButtonA.onClick.RemoveAllListeners();
            choiceButtonA.onClick.AddListener(() => OnChoiceSelected(true));
        }
        if (choiceButtonB != null)
        {
            if (choiceLabelB != null) choiceLabelB.text = currentScenario.choiceBLabel;
            choiceButtonB.onClick.RemoveAllListeners();
            choiceButtonB.onClick.AddListener(() => OnChoiceSelected(false));
        }
        if (choiceGroup != null) choiceGroup.SetActive(true);
        if (dialogueText != null) dialogueText.text = "どうする？";

        // 選択肢ボタンがどちらも無いと進行不能になるため、Aを選んだ扱いで先へ進める
        if (choiceButtonA == null && choiceButtonB == null)
        {
            Debug.LogWarning("VillagerEventPanel: 選択肢ボタンが未設定のため、自動で選択Aを適用します。");
            OnChoiceSelected(true);
        }
    }

    private void OnChoiceSelected(bool isA)
    {
        if (choiceGroup != null) choiceGroup.SetActive(false);
        string type    = isA ? currentScenario.resultTypeA  : currentScenario.resultTypeB;
        int    amount  = isA ? currentScenario.rewardAmountA : currentScenario.rewardAmountB;
        string message = isA ? currentScenario.resultMessageA : currentScenario.resultMessageB;
        QuestEventResult result = BuildResult(type, amount);
        ShowResult(message, result);
    }

    private QuestEventResult BuildResult(string type, int amount)
    {
        var r = new QuestEventResult { success = true };
        switch (type)
        {
            case "hp":   r.hpChange    = amount;  break;
            case "gold": r.goldGained  = amount;  break;
            case "exp":  r.expGained   = amount;  break;
            case "trap":
                if (amount < 0) r.goldGained = amount;
                else            r.hpChange   = -amount;
                r.success = false;
                break;
        }
        return r;
    }

    private void ShowResult(string message, QuestEventResult result)
    {
        if (resultText != null) resultText.text = message;
        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                if (resultBox != null) resultBox.SetActive(false);
                onComplete?.Invoke(result);
            });
        }
        else
        {
            // OKボタンが無いとクエストが進行不能になるため、その場で完了扱いにする
            Debug.LogWarning("VillagerEventPanel: okButton が未設定のため、結果表示をスキップして進行します。");
            onComplete?.Invoke(result);
            return;
        }

        if (resultBox != null) resultBox.SetActive(true);
    }

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }
}
