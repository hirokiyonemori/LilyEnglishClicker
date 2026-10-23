using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ピップ（マスコット）がタイプライターでヒントを表示・自動消滅するコンポーネント
/// </summary>
public class PipHintController : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject      root;
    [SerializeField] private Image           pipImage;
    [SerializeField] private GameObject      speechBubble;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("スプライト")]
    [SerializeField] private Sprite pipNormalSprite;
    [SerializeField] private Sprite pipExcitedSprite;

    [Header("表示設定")]
    [SerializeField] private float displayDuration = 3.5f;
    [SerializeField] private float typewriterSpeed = 0.04f;

    private Coroutine currentRoutine;
    private CanvasGroup bubbleCG;

    private void Awake()
    {
        if (speechBubble != null)
        {
            speechBubble.SetActive(false);
            bubbleCG = speechBubble.GetComponent<CanvasGroup>();
            if (bubbleCG == null) bubbleCG = speechBubble.AddComponent<CanvasGroup>();
        }
    }

    // ─── 公開 API ────────────────────────────────────────────────
    public void ShowHint(string message, bool excited = false)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        if (pipImage != null)
        {
            Sprite sp = (excited && pipExcitedSprite != null) ? pipExcitedSprite : pipNormalSprite;
            if (sp != null) pipImage.sprite = sp;
        }

        if (root != null) root.SetActive(true);
        if (speechBubble != null) speechBubble.SetActive(true);
        if (bubbleCG != null) bubbleCG.alpha = 1f;

        currentRoutine = StartCoroutine(HintRoutine(message));
    }

    public void HideHint()
    {
        if (currentRoutine != null) { StopCoroutine(currentRoutine); currentRoutine = null; }
        if (speechBubble != null) speechBubble.SetActive(false);
    }

    // ─── 内部 ────────────────────────────────────────────────────
    private IEnumerator HintRoutine(string message)
    {
        if (hintText != null)
        {
            hintText.text = "";
            foreach (char c in message)
            {
                hintText.text += c;
                yield return new WaitForSeconds(typewriterSpeed);
            }
        }

        yield return new WaitForSeconds(displayDuration);
        yield return StartCoroutine(FadeOut());

        if (speechBubble != null) speechBubble.SetActive(false);
        currentRoutine = null;
    }

    private IEnumerator FadeOut()
    {
        if (bubbleCG == null) yield break;
        float elapsed = 0f, dur = 0.4f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            bubbleCG.alpha = 1f - elapsed / dur;
            yield return null;
        }
        bubbleCG.alpha = 1f;
    }

    // ─── ピップのボブ演出 ────────────────────────────────────────
    private void Update()
    {
        if (pipImage == null || root == null || !root.activeSelf) return;
        RectTransform rt = pipImage.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                          Mathf.Round(Mathf.Sin(Time.time * 3f) * 4f));
    }
}
