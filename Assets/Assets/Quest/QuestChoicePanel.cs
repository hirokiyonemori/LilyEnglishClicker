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
    [SerializeField] private TextMeshProUGUI leftDescriptionText;
    [SerializeField] private string leftTitle = "森の小道";
    [SerializeField, TextArea(1, 3)] private string leftDescription = "静かな道が続いている";

    [Header("右カード")]
    [SerializeField] private Button          rightButton;
    [SerializeField] private Image           rightIcon;
    [SerializeField] private TextMeshProUGUI rightLabel;
    [SerializeField] private TextMeshProUGUI rightDescriptionText;
    [SerializeField] private string rightTitle = "古い街道";
    [SerializeField, TextArea(1, 3)] private string rightDescription = "何か見つかりそうだ";

    [Header("アイコン画像（null なら色で代用）")]
    [SerializeField] private Sprite iconQuestion;

    [Header("アニメーション")]
    [SerializeField] private float slideInDuration = 0.3f;

    private Action<bool> onChoiceSelected;
    private bool visualDefaultsCaptured;
    private bool isShowing;
    private Vector2 rootPosition;
    private Color leftIconColor;
    private Color rightIconColor;

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(int currentStep, int totalSteps, Action<bool> callback)
    {
        StopAllCoroutines();
        CaptureVisualDefaults();
        RestoreVisuals();
        isShowing = true;
        onChoiceSelected = callback;

        if (headerText != null) headerText.text = "どちらの道を選ぶ？";

        if (stepText != null)
        {
            string[] labels = { "1つ目の分岐", "2つ目の分岐", "3つ目の分岐" };
            string label = currentStep < labels.Length ? labels[currentStep] : $"分岐 {currentStep + 1}";
            stepText.text = $"{label} / あと{totalSteps - currentStep}イベント";
        }

        if (leftIcon  != null && iconQuestion != null) leftIcon.sprite  = iconQuestion;
        if (rightIcon != null && iconQuestion != null) rightIcon.sprite = iconQuestion;
        // 道ごとのイラスト（Resources/Backgrounds）。画像が無い場合は従来の絵のまま
        Sprite leftArt  = QuestBackgrounds.Get(QuestBackgrounds.PathForest);
        Sprite rightArt = QuestBackgrounds.Get(QuestBackgrounds.PathRoad);
        if (leftIcon  != null && leftArt  != null) leftIcon.sprite  = leftArt;
        if (rightIcon != null && rightArt != null) rightIcon.sprite = rightArt;
        if (leftLabel != null) leftLabel.text = leftTitle;
        if (rightLabel != null) rightLabel.text = rightTitle;
        if (leftDescriptionText != null) leftDescriptionText.text = leftDescription;
        if (rightDescriptionText != null) rightDescriptionText.text = rightDescription;

        if (leftButton != null)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(() => OnSelected(true));
        }
        if (rightButton != null)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(() => OnSelected(false));
        }

        if (root != null) root.SetActive(true);
        StartCoroutine(SlideIn());

        // ボタンがどちらも無いと進行不能になるため、左を選んだ扱いで先へ進める
        if (leftButton == null && rightButton == null)
        {
            Debug.LogWarning("QuestChoicePanel: 選択ボタンが未設定のため、自動で左を選択します。");
            OnSelected(true);
        }
    }

    public void Hide()
    {
        StopAllCoroutines();
        RestoreVisuals();
        // 次イベントは既存仕様で直ちに表示されるため、旧カードを重ねない。
        if (root != null) root.SetActive(false);
    }

    // ─── 内部 ────────────────────────────────────────────────────
    private void OnSelected(bool isLeft)
    {
        if (leftButton != null) leftButton.onClick.RemoveAllListeners();
        if (rightButton != null) rightButton.onClick.RemoveAllListeners();
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
        RectTransform rt = root != null ? root.GetComponent<RectTransform>() : null;
        if (rt == null) yield break;
        yield return Slide(rt, rootPosition + new Vector2(0f, -300f), rootPosition);
    }

    private IEnumerator SlideOut(Action onDone)
    {
        RectTransform rt = root != null ? root.GetComponent<RectTransform>() : null;
        if (rt == null) { onDone?.Invoke(); yield break; }
        yield return Slide(rt, rt.anchoredPosition, rootPosition + new Vector2(0f, 300f));
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

    private void Awake()
    {
        if (!isShowing && root != null) root.SetActive(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isShowing = false;
        RestoreVisuals();
    }

    private void CaptureVisualDefaults()
    {
        if (visualDefaultsCaptured) return;
        RectTransform rt = root != null ? root.GetComponent<RectTransform>() : null;
        if (rt != null) rootPosition = rt.anchoredPosition;
        if (leftIcon != null) leftIconColor = leftIcon.color;
        if (rightIcon != null) rightIconColor = rightIcon.color;
        visualDefaultsCaptured = true;
    }

    private void RestoreVisuals()
    {
        if (!visualDefaultsCaptured) return;
        RectTransform rt = root != null ? root.GetComponent<RectTransform>() : null;
        if (rt != null) rt.anchoredPosition = rootPosition;
        RestoreIconColors();
    }

    private void RestoreIconColors()
    {
        if (!visualDefaultsCaptured) return;
        if (leftIcon != null) leftIcon.color = leftIconColor;
        if (rightIcon != null) rightIcon.color = rightIconColor;
    }
}
