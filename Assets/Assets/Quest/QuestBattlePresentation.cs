using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ターンごとのことば魔法クイズと、初回正解ボーナスを戦闘へつなぐ。
/// HP・ターン・報酬の更新はすべて BattlePanel が担当する。
/// </summary>
public class QuestBattlePresentation : MonoBehaviour
{
    [Header("二択クイズ")]
    [SerializeField] private TextMeshProUGUI questionPrompt;
    [SerializeField] private Button answerAButton;
    [SerializeField] private TextMeshProUGUI answerAText;
    [SerializeField] private Button answerBButton;
    [SerializeField] private TextMeshProUGUI answerBText;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("敵ステータス")]
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private RectTransform enemyHpBar;
    [SerializeField, Range(0f, 0.49f)] private float bossHpBarHorizontalInset = 0.08f;
    [SerializeField, Min(0f)] private float bossHpBarExtraHeight = 8f;

    [Header("Lily ステータス")]
    [SerializeField] private TextMeshProUGUI lilyHpText;
    [SerializeField] private Slider lilyHpSlider;
    [SerializeField] private TextMeshProUGUI lilyNameText;
    [SerializeField, Min(1)] private int presentationLevel = 1;
    [SerializeField] private TextMeshProUGUI turnText;

    private BattlePanel battle;
    private Button legacyAttackButton;
    private int correctAnswerIndex;
    private bool answerSubmitted;
    private bool answeredIncorrectly;
    private QuestWordData currentWord;
    private QuestWordData openingWord;
    // 出題の乱数は、道・宝箱などの抽選から独立させる。
    private readonly System.Random quizRandom = new System.Random();
    private readonly QuestWordData[] words = QuestWordList.GetAll();
    /// <summary>有効な回答だけ通知する。実機比較用の計測に使用し、ゲームの報酬には影響しない。</summary>
    public event System.Action<bool> AnswerEvaluated;
    public RectTransform MagicOrigin
    {
        get
        {
            if (lilyNameText == null) return null;
            Transform parent = lilyNameText.transform.parent;
            Transform portrait = parent != null ? parent.Find("LilyImage") : null;
            return portrait != null ? portrait as RectTransform : lilyNameText.rectTransform;
        }
    }
    private BattleTurnState lastTurn = BattleTurnState.Hidden;
    private bool enemyBarLayoutCaptured;
    private Vector2 normalBarAnchorMin;
    private Vector2 normalBarAnchorMax;
    private Vector2 normalBarOffsetMin;
    private Vector2 normalBarOffsetMax;
    private CanvasGroup legacyAttackCanvasGroup;
    private float legacyAttackAlpha;
    private bool legacyAttackInteractable;
    private bool legacyAttackBlocksRaycasts;

    public void Show(BattlePhase phase, QuestWordData word, BattlePanel battlePanel, Button attackButton)
    {
        battle = battlePanel;
        legacyAttackButton = attackButton;
        answerSubmitted = false;
        answeredIncorrectly = false;
        openingWord = word;
        lastTurn = BattleTurnState.Hidden;

        ConfigureMode(battle.UseLegacyQuiz);
        if (battle.UseLegacyQuiz) PrepareQuestion();
        var growth = LilyEnglish.Growth.GrowthService.Instance;
        int level = growth != null ? growth.MainLevel : presentationLevel;
        if (lilyNameText != null) lilyNameText.text = $"Lily  Lv.{Mathf.Max(1, level)}";

        if (answerAButton != null)
        {
            answerAButton.onClick.RemoveListener(OnAnswerA);
            answerAButton.onClick.AddListener(OnAnswerA);
        }
        if (answerBButton != null)
        {
            answerBButton.onClick.RemoveListener(OnAnswerB);
            answerBButton.onClick.AddListener(OnAnswerB);
        }

        ApplyEnemyBarLayout(phase == BattlePhase.Boss);
        if (battle.UseLegacyQuiz) HideLegacyAttackButton();
        else RestoreLegacyAttackButton();
        Refresh();
    }

    private void ConfigureMode(bool quizMode)
    {
        if (questionPrompt != null) questionPrompt.transform.parent.gameObject.SetActive(quizMode);
        if (feedbackText != null) feedbackText.gameObject.SetActive(quizMode);
        if (legacyAttackButton == null || (battle != null && battle.HasCommandMenu)) return;
        var group = legacyAttackButton.GetComponent<CanvasGroup>();
        if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
        var rect = legacyAttackButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.12f, .34f);
        rect.anchorMax = new Vector2(.88f, .46f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public void Refresh()
    {
        if (battle == null) return;

        if (enemyHpText != null) enemyHpText.text = $"HP {battle.EnemyHp} / {battle.EnemyMaxHp}";
        if (lilyHpText != null) lilyHpText.text = $"HP {battle.PlayerHp} / {battle.PlayerMaxHp}";
        if (lilyHpSlider != null)
        {
            lilyHpSlider.minValue = 0f;
            lilyHpSlider.maxValue = 1f;
            lilyHpSlider.value = (float)battle.PlayerHp / Mathf.Max(1, battle.PlayerMaxHp);
        }

        BattleTurnState turn = battle.CurrentTurn;
        if (turn != lastTurn)
        {
            if (turn == BattleTurnState.PlayerTurn)
            {
                if (battle.UseLegacyQuiz && lastTurn == BattleTurnState.EnemyTurn) PrepareQuestion();
                answerSubmitted = false;
                if (feedbackText != null) feedbackText.text = "一発正解で魔法の威力2倍！";
            }
            else if (turn == BattleTurnState.Victory)
            {
                if (feedbackText != null) feedbackText.text = "敵を倒した！";
            }
            else if (turn == BattleTurnState.Defeat)
            {
                if (feedbackText != null) feedbackText.text = "Lilyは力尽きてしまった…";
            }
            lastTurn = turn;
        }

        bool canAnswer = battle.UseLegacyQuiz && turn == BattleTurnState.PlayerTurn && !answerSubmitted &&
                         legacyAttackButton != null && legacyAttackButton.interactable;
        if (answerAButton != null) answerAButton.interactable = canAnswer;
        if (answerBButton != null) answerBButton.interactable = canAnswer;
        if (turnText != null) turnText.text = TurnLabel(turn);
    }

    // ─── Lily が敵の攻撃を受けた時の演出 ─────────────────────────────
    private Coroutine hitRoutine;
    private Image hitPortrait;
    private Vector2 hitPortraitBasePosition;
    private Color hitPortraitBaseColor;
    private TextMeshProUGUI lilyDamageText;

    /// <summary>
    /// 敵の攻撃で Lily が受けたダメージを、立ち絵の揺れ・赤い点滅・「-N」の数字で見せる。
    /// HP やターンの進行は BattlePanel が持ち、ここは見た目だけを担当する。
    /// </summary>
    public void PlayLilyHit(int damage, bool guarded)
    {
        if (!isActiveAndEnabled) return;
        StopLilyHit();
        RectTransform anchor = MagicOrigin;
        hitPortrait = anchor != null ? anchor.GetComponent<Image>() : null;
        if (hitPortrait != null)
        {
            hitPortraitBasePosition = hitPortrait.rectTransform.anchoredPosition;
            hitPortraitBaseColor = hitPortrait.color;
        }
        hitRoutine = StartCoroutine(LilyHitRoutine(anchor, damage, guarded));
    }

    private System.Collections.IEnumerator LilyHitRoutine(RectTransform anchor, int damage, bool guarded)
    {
        TextMeshProUGUI label = EnsureDamageText(anchor);
        Vector2 labelStart = Vector2.zero;
        if (label != null)
        {
            label.text = guarded ? $"-{damage}\n<size=55%>ガード！</size>" : $"-{damage}";
            label.color = guarded ? new Color(.62f, .86f, 1f, 1f) : new Color(1f, .33f, .28f, 1f);
            label.gameObject.SetActive(true);
            if (anchor != null) label.rectTransform.anchoredPosition = anchor.anchoredPosition + new Vector2(0f, anchor.rect.height * 0.15f);
            labelStart = label.rectTransform.anchoredPosition;
        }

        const float duration = 0.9f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            if (hitPortrait != null)
            {
                // 最初の 0.3 秒だけ左右に揺れ、赤く染まってから元の色へ戻る
                float shake = p < 0.33f ? Mathf.Sin(p * 80f) * 16f * (1f - p / 0.33f) : 0f;
                hitPortrait.rectTransform.anchoredPosition = hitPortraitBasePosition + new Vector2(shake, 0f);
                Color tint = guarded ? new Color(.7f, .85f, 1f, hitPortraitBaseColor.a) : new Color(1f, .45f, .45f, hitPortraitBaseColor.a);
                hitPortrait.color = Color.Lerp(tint, hitPortraitBaseColor, Mathf.Clamp01(p / 0.6f));
            }
            if (label != null)
            {
                label.rectTransform.anchoredPosition = labelStart + new Vector2(0f, p * 80f);
                Color c = label.color; c.a = 1f - Mathf.Clamp01((p - 0.35f) / 0.65f); label.color = c;
            }
            yield return null;
        }
        StopLilyHit();
    }

    private TextMeshProUGUI EnsureDamageText(RectTransform anchor)
    {
        if (lilyDamageText != null) return lilyDamageText;
        Transform parent = anchor != null ? anchor.parent : (lilyNameText != null ? lilyNameText.transform.parent : null);
        if (parent == null || lilyNameText == null) return null;
        var go = new GameObject("LilyDamageFloat", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        lilyDamageText = go.AddComponent<TextMeshProUGUI>();
        lilyDamageText.font = lilyNameText.font;
        lilyDamageText.fontSize = 64f;
        lilyDamageText.fontStyle = FontStyles.Bold;
        lilyDamageText.alignment = TextAlignmentOptions.Center;
        lilyDamageText.textWrappingMode = TextWrappingModes.NoWrap;
        lilyDamageText.raycastTarget = false;
        lilyDamageText.rectTransform.sizeDelta = new Vector2(360f, 140f);
        go.transform.SetAsLastSibling();
        return lilyDamageText;
    }

    private void StopLilyHit()
    {
        if (hitRoutine != null) { StopCoroutine(hitRoutine); hitRoutine = null; }
        if (hitPortrait != null)
        {
            hitPortrait.rectTransform.anchoredPosition = hitPortraitBasePosition;
            hitPortrait.color = hitPortraitBaseColor;
            hitPortrait = null;
        }
        if (lilyDamageText != null) lilyDamageText.gameObject.SetActive(false);
    }

    private void OnAnswerA() { Answer(0); }
    private void OnAnswerB() { Answer(1); }

    private void Answer(int answerIndex)
    {
        if (!isActiveAndEnabled || battle == null || !battle.UseLegacyQuiz || !battle.isActiveAndEnabled ||
            battle.CurrentTurn != BattleTurnState.PlayerTurn || answerSubmitted ||
            legacyAttackButton == null || !legacyAttackButton.interactable) return;

        AnswerEvaluated?.Invoke(answerIndex == correctAnswerIndex);
        if (answerIndex != correctAnswerIndex)
        {
            answeredIncorrectly = true;
            // 不正解は再回答。敵だけを動かす等の新しい戦闘ルールは追加しない。
            if (feedbackText != null) feedbackText.text = "おしい！ もう一度選んでみよう";
            return;
        }

        answerSubmitted = true;
        var growth = LilyEnglish.Growth.GrowthService.Instance;
        if (growth != null) growth.OnCorrectAnswer(); // 英単語の正解で EXP
        bool critical = !answeredIncorrectly;
        if (feedbackText != null) feedbackText.text = critical ? "一発正解！ CRITICAL！" : "正解！ ことば魔法！";
        Refresh();
        battle.TryCastWordMagic(critical);
    }

    private void PrepareQuestion()
    {
        string previous = currentWord != null ? currentWord.english : null;
        if (openingWord != null && !string.IsNullOrEmpty(openingWord.meaning) && openingWord.english != previous)
            currentWord = openingWord;
        else
        {
            // 直前の単語を候補から除き、同じ敵でも毎ターン別のことばを唱える。
            var candidates = System.Array.FindAll(words, candidate => candidate != null &&
                !string.IsNullOrEmpty(candidate.meaning) && candidate.english != previous);
            if (candidates.Length > 0) currentWord = candidates[quizRandom.Next(candidates.Length)];
        }
        openingWord = null;
        answeredIncorrectly = false;
        string meaning = currentWord != null ? currentWord.meaning : "意味が未設定";
        correctAnswerIndex = quizRandom.Next(2);
        string distractor = FindDistractor(meaning);
        if (answerAText != null) answerAText.text = correctAnswerIndex == 0 ? meaning : distractor;
        if (answerBText != null) answerBText.text = correctAnswerIndex == 1 ? meaning : distractor;
        if (questionPrompt != null) questionPrompt.text = "ことば魔法：意味を選ぼう";
        if (feedbackText != null) feedbackText.text = "一発正解で魔法の威力2倍！";
        if (battle != null) battle.SetSpellWord(currentWord);
    }

    private string FindDistractor(string correctMeaning)
    {
        int start = quizRandom.Next(words.Length);
        for (int i = 0; i < words.Length; i++)
        {
            QuestWordData candidate = words[(start + i) % words.Length];
            if (candidate != null && !string.IsNullOrEmpty(candidate.meaning) && candidate.meaning != correctMeaning)
                return candidate.meaning;
        }
        return "別の意味";
    }

    private static string TurnLabel(BattleTurnState turn)
    {
        switch (turn)
        {
            case BattleTurnState.Entering: return "敵が現れた！";
            case BattleTurnState.PlayerTurn: return "Lily のターン";
            case BattleTurnState.PlayerAttacking: return "Lily の攻撃";
            case BattleTurnState.EnemyTurn: return "敵のターン";
            case BattleTurnState.Victory: return "VICTORY";
            case BattleTurnState.Defeat: return "DEFEAT";
            default: return "";
        }
    }

    private void ApplyEnemyBarLayout(bool isBoss)
    {
        if (enemyHpBar == null) return;
        if (!enemyBarLayoutCaptured)
        {
            normalBarAnchorMin = enemyHpBar.anchorMin;
            normalBarAnchorMax = enemyHpBar.anchorMax;
            normalBarOffsetMin = enemyHpBar.offsetMin;
            normalBarOffsetMax = enemyHpBar.offsetMax;
            enemyBarLayoutCaptured = true;
        }
        float inset = Mathf.Clamp(bossHpBarHorizontalInset, 0f, 0.49f);
        float extraHeight = Mathf.Max(0f, bossHpBarExtraHeight) * 0.5f;
        enemyHpBar.anchorMin = isBoss ? new Vector2(inset, normalBarAnchorMin.y) : normalBarAnchorMin;
        enemyHpBar.anchorMax = isBoss ? new Vector2(1f - inset, normalBarAnchorMax.y) : normalBarAnchorMax;
        enemyHpBar.offsetMin = isBoss ? new Vector2(0f, normalBarOffsetMin.y - extraHeight) : normalBarOffsetMin;
        enemyHpBar.offsetMax = isBoss ? new Vector2(0f, normalBarOffsetMax.y + extraHeight) : normalBarOffsetMax;
    }

    private void HideLegacyAttackButton()
    {
        if (legacyAttackButton == null) return;
        CanvasGroup group = legacyAttackButton.GetComponent<CanvasGroup>();
        if (group == null) group = legacyAttackButton.gameObject.AddComponent<CanvasGroup>();
        if (legacyAttackCanvasGroup != group)
        {
            RestoreLegacyAttackButton();
            legacyAttackCanvasGroup = group;
            legacyAttackAlpha = group.alpha;
            legacyAttackInteractable = group.interactable;
            legacyAttackBlocksRaycasts = group.blocksRaycasts;
        }
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void RestoreLegacyAttackButton()
    {
        if (legacyAttackCanvasGroup == null) return;
        legacyAttackCanvasGroup.alpha = legacyAttackAlpha;
        legacyAttackCanvasGroup.interactable = legacyAttackInteractable;
        legacyAttackCanvasGroup.blocksRaycasts = legacyAttackBlocksRaycasts;
        legacyAttackCanvasGroup = null;
    }

    private void OnDisable()
    {
        StopLilyHit();
        if (answerAButton != null) answerAButton.onClick.RemoveListener(OnAnswerA);
        if (answerBButton != null) answerBButton.onClick.RemoveListener(OnAnswerB);
        RestoreLegacyAttackButton();
    }
}
