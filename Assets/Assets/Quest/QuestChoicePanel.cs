using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// クエスト道中の二択選択パネル
/// </summary>
public class QuestChoicePanel : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI stepText;

    [Header("左カード")]
    [SerializeField] private Button          leftButton;
    [SerializeField] private Image           leftIcon;
    [SerializeField] private TextMeshProUGUI leftLabel;

    [Header("右カード")]
    [SerializeField] private Button          rightButton;
    [SerializeField] private Image           rightIcon;
    [SerializeField] private TextMeshProUGUI rightLabel;

    [Header("アイコン画像（null なら色で代用）")]
    [SerializeField] private Sprite iconQuestion;

    [Header("アニメーション")]
    [SerializeField] private float slideInDuration = 0.3f;

    private Action<bool> onChoiceSelected;

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(int currentStep, int totalSteps, Action<bool> callback)
    {
        onChoiceSelected = callback;

        if (headerText != null) headerText.text = "どちらの道を選ぶ？";

        if (stepText != null)
        {
            string[] labels = { "1つ目の分岐", "2つ目の分岐", "3つ目の分岐" };
            string label = currentStep < labels.Length ? labels[currentStep] : $"分岐 {currentStep + 1}";
            stepText.text = $"【{label}】あと {totalSteps - currentStep} イベント";
        }

        if (leftIcon  != null && iconQuestion != null) leftIcon.sprite  = iconQuestion;
        if (rightIcon != null && iconQuestion != null) rightIcon.sprite = iconQuestion;
        if (leftLabel  != null) leftLabel.text  = "左の道";
        if (rightLabel != null) rightLabel.text = "右の道";

        leftButton?.onClick.RemoveAllListeners();
        rightButton?.onClick.RemoveAllListeners();
        leftButton?.onClick.AddListener(() => OnSelected(true));
        rightButton?.onClick.AddListener(() => OnSelected(false));

        root?.SetActive(true);
        StartCoroutine(SlideIn());
    }

    public void Hide()
    {
        StartCoroutine(SlideOut(() => root?.SetActive(false)));
    }

    // ─── 内部 ────────────────────────────────────────────────────
    private void OnSelected(bool isLeft)
    {
        leftButton?.onClick.RemoveAllListeners();
        rightButton?.onClick.RemoveAllListeners();
        Image icon = isLeft ? leftIcon : rightIcon;
        StartCoroutine(Flash(icon, () => onChoiceSelected?.Invoke(isLeft)));
    }

    private IEnumerator Flash(Image icon, Action onDone)
    {
        if (icon != null)
        {
            Color orig = icon.color;
            icon.color = Color.yellow;
            yield return new WaitForSeconds(0.15f);
            icon.color = orig;
            yield return new WaitForSeconds(0.05f);
        }
        onDone?.Invoke();
    }

    private IEnumerator SlideIn()
    {
        RectTransform rt = root?.GetComponent<RectTransform>();
        if (rt == null) yield break;
        yield return Slide(rt, new Vector2(0f, -300f), Vector2.zero);
    }

    private IEnumerator SlideOut(Action onDone)
    {
        RectTransform rt = root?.GetComponent<RectTransform>();
        if (rt == null) { onDone?.Invoke(); yield break; }
        yield return Slide(rt, Vector2.zero, new Vector2(0f, 300f));
        onDone?.Invoke();
    }

    private IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to)
    {
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / slideInDuration));
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    private void Awake() => root?.SetActive(false);
}
