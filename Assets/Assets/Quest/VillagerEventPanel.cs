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

    [Header("ダイアログ")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject      tapPrompt;    // ▼タップで次へ

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

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(VillagerScenario scenario, Action<QuestEventResult> callback)
    {
        currentScenario = scenario;
        onComplete      = callback;
        lineIndex       = 0;

        if (villagerImage != null && defaultVillagerSprite != null)
            villagerImage.sprite = defaultVillagerSprite;

        if (speakerNameText != null) speakerNameText.text = scenario.speakerName;

        choiceGroup?.SetActive(false);
        resultBox?.SetActive(false);
        tapPrompt?.SetActive(false);

        root?.SetActive(true);
        StartCoroutine(RunDialogue());
    }

    public void Hide()
    {
        StopAllCoroutines();
        root?.SetActive(false);
    }

    // ─── ダイアログ進行 ──────────────────────────────────────────
    private IEnumerator RunDialogue()
    {
        while (lineIndex < currentScenario.lines.Length)
        {
            yield return StartCoroutine(TypeLine(currentScenario.lines[lineIndex].text));
            tapPrompt?.SetActive(true);
            yield return WaitForTap();
            tapPrompt?.SetActive(false);
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
        yield return new WaitUntil(() =>
            Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began));
        yield return null;
    }

    // ─── 選択肢 ──────────────────────────────────────────────────
    private void ShowChoices()
    {
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
        choiceGroup?.SetActive(true);
        if (dialogueText != null) dialogueText.text = "どうする？";
    }

    private void OnChoiceSelected(bool isA)
    {
        choiceGroup?.SetActive(false);
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
                resultBox?.SetActive(false);
                onComplete?.Invoke(result);
            });
        }
        resultBox?.SetActive(true);
    }

    private void Awake() => root?.SetActive(false);
}
