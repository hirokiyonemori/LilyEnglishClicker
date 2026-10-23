using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 本編下部 HUD の「⚔ 冒険」ボタン。解放中だけ押せて、押すと QuestEntryController のダイアログを開く。
/// 解放判定・進捗の保存・ダイアログの出し分けはすべて QuestEntryController が持ち、ここは表示だけを行う。
///
/// 未解放中はボタン内に撃破進捗（6 / 10）、解放後は金色の面と「!」バッジで解放済みを示す。
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class QuestHudButton : MonoBehaviour
{
    [Header("未設定ならシーンから取得")]
    public QuestEntryController entry;

    [Header("表示部品")]
    public QuestSurfaceGraphic face;
    public TMP_Text label;
    public Graphic icon;
    public GameObject progressRoot;
    public TMP_Text progressText;
    public RectTransform unlockedBadge;

    [Header("未解放時（紺＋金縁）")]
    public Color lockedTop = new Color(0.12f, 0.23f, 0.34f, 0.98f);
    public Color lockedBottom = new Color(0.06f, 0.12f, 0.23f, 0.98f);
    public Color lockedBorder = new Color(0.77f, 0.64f, 0.38f);
    public Color lockedContent = new Color(0.96f, 0.93f, 0.85f);
    public Color lockedIcon = new Color(0.85f, 0.76f, 0.55f);

    [Header("解放後（金）")]
    public Color unlockedTop = new Color(0.94f, 0.85f, 0.63f);
    public Color unlockedBottom = new Color(0.80f, 0.66f, 0.44f);
    public Color unlockedBorder = new Color(0.98f, 0.91f, 0.74f);
    public Color unlockedContent = new Color(0.09f, 0.18f, 0.26f);

    [Header("「!」バッジの脈動")]
    [Min(0f)] public float badgePulseSpeed = 4f;
    [Range(0f, 0.3f)] public float badgePulseAmount = 0.12f;

    private Button button;
    private bool dirty = true;
    private int shownDefeats = -1;
    private int shownState = -1;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        button.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        Idle.GameManager.EnemyDefeated += MarkDirty;
        // クエストから戻った時（本編 Canvas の再表示）にも最新状態へ揃える
        dirty = true;
    }

    private void OnDisable()
    {
        Idle.GameManager.EnemyDefeated -= MarkDirty;
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    /// <summary>
    /// QuestEntryController のハンドラより先に呼ばれることがあるため、ここでは印だけ付けて
    /// 同じフレームの LateUpdate（全ハンドラの処理後）で PlayerPrefs を読み直す
    /// </summary>
    private void MarkDirty()
    {
        dirty = true;
    }

    private void LateUpdate()
    {
        if (dirty || shownState != (QuestEntryController.IsUnlocked ? 1 : 0))
        {
            dirty = false;
            Refresh();
        }

        if (unlockedBadge != null && unlockedBadge.gameObject.activeSelf)
        {
            float scale = 1f + Mathf.Sin(Time.unscaledTime * badgePulseSpeed) * badgePulseAmount;
            unlockedBadge.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void OnClick()
    {
        if (!QuestEntryController.IsUnlocked) return;
        if (entry == null) entry = FindAnyObjectByType<QuestEntryController>(FindObjectsInactive.Include);
        if (entry == null)
        {
            Debug.LogError("[QuestHudButton] QuestEntryController がシーンにありません。");
            return;
        }
        entry.Open();
    }

    private void Refresh()
    {
        bool unlocked = QuestEntryController.IsUnlocked;
        button.interactable = unlocked;
        int required = QuestEntryController.RequiredDefeats;
        int defeats = Mathf.Min(QuestEntryController.DefeatCount, required);
        int state = unlocked ? 1 : 0;
        if (state == shownState && (unlocked || defeats == shownDefeats)) return;
        shownState = state;
        shownDefeats = defeats;

        if (progressRoot != null) progressRoot.SetActive(!unlocked);
        if (progressText != null) progressText.text = $"あと{required - defeats}体";
        if (unlockedBadge != null)
        {
            unlockedBadge.gameObject.SetActive(unlocked);
            unlockedBadge.localScale = Vector3.one;
        }

        Color content = unlocked ? unlockedContent : lockedContent;
        if (label != null) label.color = content;
        if (icon != null) icon.color = unlocked ? unlockedContent : lockedIcon;

        if (face != null)
        {
            face.topColor = unlocked ? unlockedTop : lockedTop;
            face.bottomColor = unlocked ? unlockedBottom : lockedBottom;
            face.borderColor = unlocked ? unlockedBorder : lockedBorder;
            face.SetVerticesDirty();
        }
    }
}
