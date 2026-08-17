using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道中の敵バトル / ボスバトル 共用パネル
/// QuestWordData を受け取り、タップで HP を削って戦う
/// </summary>
public class BattlePanel : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image      enemyImage;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI englishText;
    [SerializeField] private TextMeshProUGUI readingText;

    [Header("HP バー")]
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("攻撃ボタン")]
    [SerializeField] private Button          attackButton;
    [SerializeField] private TextMeshProUGUI attackButtonText;

    [Header("ボス専用")]
    [SerializeField] private GameObject bossLabel;

    [Header("ダメージ演出")]
    [SerializeField] private TextMeshProUGUI damageFloatText;
    [SerializeField] private Color           damageColor = Color.red;
    [SerializeField] private float           flashDuration = 0.1f;

    [Header("結果")]
    [SerializeField] private GameObject      resultBox;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button          okButton;

    [Header("スプライト")]
    [SerializeField] private Sprite defaultEnemySprite;
    [SerializeField] private Sprite bossSprite;

    // ─── ランタイム ──────────────────────────────────────────────
    private int   currentHp;
    private int   maxHp;
    private int   goldReward;
    private bool  battleEnded;
    private Action<QuestEventResult> onBattleEnd;

    // ─── 公開API ─────────────────────────────────────────────────
    public void Show(BattlePhase phase, string enemyName, QuestWordData word,
                     int hp, int goldReward, Action<QuestEventResult> onBattleEnd)
    {
        this.currentHp    = hp;
        this.maxHp        = hp;
        this.goldReward   = goldReward;
        this.onBattleEnd  = onBattleEnd;
        this.battleEnded  = false;

        bool isBoss = phase == BattlePhase.Boss;

        if (enemyNameText  != null) enemyNameText.text  = enemyName;
        if (englishText    != null) englishText.text    = word?.english ?? "???";
        if (readingText    != null) readingText.text    = word?.reading ?? "";
        if (attackButtonText != null) attackButtonText.text = isBoss ? "全力攻撃！" : "攻撃！";

        bossLabel?.SetActive(isBoss);
        if (enemyImage != null)
        {
            Sprite sp = (isBoss && bossSprite != null) ? bossSprite : defaultEnemySprite;
            if (sp != null) enemyImage.sprite = sp;
        }

        UpdateHpBar();
        resultBox?.SetActive(false);
        if (damageFloatText != null) damageFloatText.gameObject.SetActive(false);

        attackButton?.onClick.RemoveAllListeners();
        attackButton?.onClick.AddListener(OnAttackClicked);

        root?.SetActive(true);

        if (isBoss) StartCoroutine(BossEntrance());
    }

    public void Hide()
    {
        StopAllCoroutines();
        root?.SetActive(false);
    }

    // ─── 攻撃 ────────────────────────────────────────────────────
    private void OnAttackClicked()
    {
        if (battleEnded) return;

        currentHp--;
        UpdateHpBar();
        StartCoroutine(DamageFlash());
        ShowDamageNumber(1);

        // タップ音
        if (TapAttackManager.Instance != null)
            TapAttackManager.Instance.PlayHitSound();

        if (currentHp <= 0)
            StartCoroutine(OnEnemyDefeated());
    }

    // ─── HP バー ─────────────────────────────────────────────────
    private void UpdateHpBar()
    {
        if (hpSlider != null) hpSlider.value = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        if (hpText   != null) hpText.text    = $"{Mathf.Max(0, currentHp)} / {maxHp}";
    }

    // ─── 撃破 ────────────────────────────────────────────────────
    private IEnumerator OnEnemyDefeated()
    {
        battleEnded = true;
        attackButton?.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.4f);

        var result = new QuestEventResult
        {
            success    = true,
            goldGained = goldReward,
            expGained  = 50,
            message    = $"敵を倒した！\n{goldReward}G 獲得！"
        };

        if (resultText != null) resultText.text = result.message;

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                resultBox?.SetActive(false);
                onBattleEnd?.Invoke(result);
            });
        }
        resultBox?.SetActive(true);
    }

    // ─── ダメージフラッシュ ──────────────────────────────────────
    private IEnumerator DamageFlash()
    {
        if (enemyImage == null) yield break;
        Color orig = enemyImage.color;
        enemyImage.color = damageColor;
        yield return new WaitForSeconds(flashDuration);
        enemyImage.color = orig;
    }

    // ─── ダメージ数値フロート ────────────────────────────────────
    private void ShowDamageNumber(int damage)
    {
        if (damageFloatText == null) return;
        damageFloatText.text = "-" + damage;
        damageFloatText.gameObject.SetActive(true);
        StartCoroutine(FloatAndFade(damageFloatText));
    }

    private IEnumerator FloatAndFade(TextMeshProUGUI t)
    {
        RectTransform rt  = t.GetComponent<RectTransform>();
        Vector2 origin    = rt != null ? rt.anchoredPosition : Vector2.zero;
        float duration    = 0.8f;
        float elapsed     = 0f;
        Color c           = t.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            if (rt != null) rt.anchoredPosition = origin + new Vector2(0f, p * 60f);
            t.color = new Color(c.r, c.g, c.b, 1f - p);
            yield return null;
        }
        t.color = new Color(c.r, c.g, c.b, 1f);
        if (rt != null) rt.anchoredPosition = origin;
        t.gameObject.SetActive(false);
    }

    // ─── ボス登場スライドイン ────────────────────────────────────
    private IEnumerator BossEntrance()
    {
        if (enemyImage == null) yield break;
        RectTransform rt = enemyImage.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 end     = rt.anchoredPosition;
        Vector2 start   = end + new Vector2(400f, 0f);
        float   elapsed = 0f, dur = 0.5f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / dur));
            yield return null;
        }
        rt.anchoredPosition = end;
    }

    private void Awake() => root?.SetActive(false);
}
