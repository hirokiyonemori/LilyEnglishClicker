using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// クエストクリア / ゲームオーバー 結果パネル
/// </summary>
public class QuestClearPanel : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject      root;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private Image           lilyImage;
    [SerializeField] private TextMeshProUGUI clearMessageText;
    [SerializeField] private PipHintController pipHint;

    [Header("スタッツ")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI itemText;

    [Header("ボタン")]
    [SerializeField] private Button          closeButton;
    [SerializeField] private TextMeshProUGUI closeButtonLabel;

    [Header("スプライト")]
    [SerializeField] private Sprite lilyHappySprite;
    [SerializeField] private Sprite lilySadSprite;

    [Header("SE")]
    [SerializeField] private AudioClip clearSE;
    [SerializeField] private AudioClip gameOverSE;

    [Header("演出")]
    [SerializeField] private float countUpDuration = 1.5f;

    private AudioSource audioSource;
    private Action onClose;
    private bool isShowing;

    private void Awake()
    {
        if (!isShowing && root != null) root.SetActive(false);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(string questName, string clearMessage,
                     int totalGold, int totalExp, Action onClose)
    {
        if (pipHint != null) pipHint.HideHint();
        PrepareShow();
        this.onClose = onClose;

        if (titleText       != null) titleText.text       = "QUEST CLEAR!";
        if (questNameText   != null) questNameText.text   = questName;
        if (clearMessageText != null) clearMessageText.text = clearMessage;
        if (lilyImage != null && lilyHappySprite != null) lilyImage.sprite = lilyHappySprite;
        if (goldText != null) goldText.text = "Gold +0";
        if (expText != null) expText.text = "EXP +0";
        if (itemText != null) itemText.text = "";

        SetupButton("冒険を終える");
        PlaySE(clearSE);

        StartCoroutine(ShowStats(totalGold, totalExp));
    }

    public void ShowGameOver(Action onClose)
    {
        if (pipHint != null) pipHint.HideHint();
        PrepareShow();
        this.onClose = onClose;

        if (titleText       != null) titleText.text       = "力尽きてしまった…";
        if (questNameText   != null) questNameText.text   = "";
        if (clearMessageText != null) clearMessageText.text = "リリィ：「次はもっと頑張るよ！」";
        if (lilyImage != null && lilySadSprite != null)   lilyImage.sprite = lilySadSprite;
        if (goldText  != null) goldText.text = "Gold +0";
        if (expText   != null) expText.text  = "EXP +0";
        if (itemText  != null) itemText.text = "";

        SetupButton("冒険を終える");
        PlaySE(gameOverSE);
    }

    public void Hide()
    {
        StopAllCoroutines();
        isShowing = false;
        if (root != null) root.SetActive(false);
    }

    private void PrepareShow()
    {
        StopAllCoroutines();
        isShowing = true;
        if (root != null) root.SetActive(true);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isShowing = false;
    }

    // ─── 内部 ────────────────────────────────────────────────────
    private void SetupButton(string label)
    {
        if (closeButtonLabel != null) closeButtonLabel.text = label;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => { Hide(); onClose?.Invoke(); });
        }
        else
        {
            // 閉じるボタンが無いとクエスト状態が Idle に戻らず本編が操作不能になるため、
            // 少し見せてから自動で閉じる
            Debug.LogWarning("QuestClearPanel: closeButton が未設定のため、自動で閉じます。");
            StartCoroutine(AutoClose());
        }
    }

    private IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(3f);
        Hide();
        onClose?.Invoke();
    }

    private IEnumerator ShowStats(int gold, int exp)
    {
        yield return new WaitForSeconds(0.5f);

        if (goldText != null)
            yield return StartCoroutine(CountUp(goldText, 0, gold, countUpDuration,
                                                v => $"Gold +{v:N0}"));

        yield return new WaitForSeconds(0.2f);
        if (expText  != null) expText.text  = $"EXP +{exp:N0}";
        if (itemText != null) itemText.text = "";

        yield return new WaitForSeconds(0.3f);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
    }

    private IEnumerator CountUp(TextMeshProUGUI t, int from, int to,
                                float duration, Func<int, string> format)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - elapsed / duration, 3f);
            t.text = format((int)Mathf.Lerp(from, to, p));
            yield return null;
        }
        t.text = format(to);
    }

    private void PlaySE(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }
}
