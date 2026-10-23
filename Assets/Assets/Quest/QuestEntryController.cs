using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒険クエストの入口モーダルダイアログ。本編下部 HUD の「⚔ 冒険」ボタンから Open() で開く。
///
/// 本編で敵を <see cref="RequiredDefeats"/> 体倒すと解放され、解放状態は PlayerPrefs に保存する
/// （既存セーブ data.bin の構造には手を入れない）。解放した瞬間だけダイアログを自動表示する。
/// 「冒険に出る」ボタンは QuestOverlayController.openButton として配線されており、
/// クエストの開始・進行は既存の QuestOverlayController / QuestManager がそのまま担当する。
/// </summary>
public sealed class QuestEntryController : MonoBehaviour
{
    public const int RequiredDefeats = 10;
    public const string UnlockedKey = "LilyClicker.Quest.Unlocked";
    public const string DefeatCountKey = "LilyClicker.Quest.DefeatsTowardUnlock";

    [Header("開くボタン（任意）。本編 HUD の「⚔ 冒険」は Open() を直接呼ぶので通常は未設定")]
    public Button entryButton;

    [Header("ダイアログ")]
    public GameObject dialogRoot;
    public CanvasGroup dialogGroup;
    public TextMeshProUGUI headlineText;
    public TextMeshProUGUI progressText;
    public Slider progressBar;
    public TextMeshProUGUI hintText;
    public TextMeshProUGUI descriptionText;
    public Button adventureButton;
    public Button closeButton;

    [Tooltip("表示直後の誤タップ防止。敵を連打している最中に自動表示されるため、少しの間ボタンを無効にする")]
    [SerializeField, Min(0f)] private float inputGuardSeconds = 0.6f;

    private Coroutine guardRoutine;

    public static bool IsUnlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;
    public static int DefeatCount => PlayerPrefs.GetInt(DefeatCountKey, 0);
    /// <summary>クリア後は本編の敵を再び10体倒すまで冒険を休止する。</summary>
    public static void ResetAfterClear()
    {
        PlayerPrefs.SetInt(UnlockedKey, 0);
        PlayerPrefs.SetInt(DefeatCountKey, 0);
        PlayerPrefs.Save();
    }

    public bool IsDialogOpen => dialogRoot != null && dialogRoot.activeSelf;

    private void Awake()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
    }

    private void Start()
    {
        if (entryButton != null) entryButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        // クエスト開始は QuestOverlayController 側のリスナーが行う。ここではダイアログを閉じるだけ
        if (adventureButton != null) adventureButton.onClick.AddListener(Close);
        Idle.GameManager.EnemyDefeated += HandleEnemyDefeated;
    }

    private void OnDestroy()
    {
        Idle.GameManager.EnemyDefeated -= HandleEnemyDefeated;
        if (entryButton != null) entryButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (adventureButton != null) adventureButton.onClick.RemoveListener(Close);
    }

    private void Update()
    {
        // Android の戻るボタンで閉じる
        if (IsDialogOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void HandleEnemyDefeated()
    {
        if (IsUnlocked || QuestManager.IsQuestActive) return;

        int count = DefeatCount + 1;
        PlayerPrefs.SetInt(DefeatCountKey, count);
        if (count >= RequiredDefeats)
        {
            PlayerPrefs.SetInt(UnlockedKey, 1);
            PlayerPrefs.Save();
            Open(); // 解放した瞬間だけ自動表示（以降は IsUnlocked で早期 return する）
            return;
        }
        PlayerPrefs.Save();
        if (IsDialogOpen) Refresh();
    }

    public void Open()
    {
        if (dialogRoot == null) return;
        if (QuestManager.IsQuestActive) return;

        dialogRoot.SetActive(true);
        Refresh();

        if (dialogGroup != null)
        {
            if (guardRoutine != null) StopCoroutine(guardRoutine);
            guardRoutine = StartCoroutine(InputGuard());
        }
    }

    public void Close()
    {
        if (guardRoutine != null) { StopCoroutine(guardRoutine); guardRoutine = null; }
        if (dialogGroup != null) dialogGroup.interactable = true;
        if (dialogRoot != null) dialogRoot.SetActive(false);
    }

    private IEnumerator InputGuard()
    {
        dialogGroup.interactable = false;
        yield return new WaitForSecondsRealtime(inputGuardSeconds);
        dialogGroup.interactable = true;
        guardRoutine = null;
    }

    private void Refresh()
    {
        bool unlocked = IsUnlocked;
        int shown = Mathf.Min(DefeatCount, RequiredDefeats);

        if (headlineText != null) headlineText.text = unlocked ? "冒険クエストが解放されました！" : "冒険クエスト解放まで";
        if (progressText != null)
        {
            progressText.gameObject.SetActive(!unlocked);
            progressText.text = $"敵 {shown} / {RequiredDefeats}体";
        }
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(!unlocked);
            progressBar.value = (float)shown / RequiredDefeats;
        }
        if (hintText != null) hintText.gameObject.SetActive(!unlocked);
        if (descriptionText != null) descriptionText.gameObject.SetActive(unlocked);
        if (adventureButton != null) adventureButton.gameObject.SetActive(unlocked);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
    }
}
