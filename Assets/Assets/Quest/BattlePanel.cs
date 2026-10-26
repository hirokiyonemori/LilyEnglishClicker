using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum BattleTurnState { Hidden, Entering, PlayerTurn, PlayerAttacking, EnemyTurn, Victory, Defeat }

/// <summary>道中 / ボス共通のターン制バトル。戦闘結果の確認時に HP 差分を一度だけ返す。</summary>
public class BattlePanel : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image enemyImage;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI englishText;
    [SerializeField] private TextMeshProUGUI readingText;

    [Header("HP バー")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("攻撃ボタン")]
    [SerializeField] private Button attackButton;
    [SerializeField] private TextMeshProUGUI attackButtonText;

    [Header("ボス専用")]
    [SerializeField] private GameObject bossLabel;

    [Header("ダメージ演出")]
    [SerializeField] private TextMeshProUGUI damageFloatText;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    [Header("ターン間隔")]
    [SerializeField, Min(0f)] private float spellTravelDuration = 0.24f;
    [SerializeField, Min(0f)] private float turnDelay = 0.22f;
    [SerializeField, Min(0f)] private float resultDelay = 0.4f;

    [Header("結果")]
    [SerializeField] private GameObject resultBox;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button okButton;

    [Header("スプライト")]
    [SerializeField] private Sprite defaultEnemySprite;
    [SerializeField] private Sprite bossSprite;

    [Header("クイズとステータス表示（任意）")]
    [SerializeField] private QuestBattlePresentation presentation;

    // 本番Scene/Prefabではfalse。旧クイズの回帰検証だけが明示的に有効化する。
    public bool UseLegacyQuiz { get; set; }

    public BattleTurnState CurrentTurn { get; private set; } = BattleTurnState.Hidden;
    public int EnemyHp { get; private set; }
    public int EnemyMaxHp => maxHp;
    public int PlayerHp { get; private set; }
    public int PlayerMaxHp { get; private set; }
    public int PlayerAttack { get; private set; }
    public int EnemyAttack { get; private set; }
    public float LastSpellTravelSeconds { get; private set; }
    [Header("冒険コマンド")]
    [SerializeField, Min(1)] private int baseMaxMp = 5;
    [SerializeField] private BattleCommandMenu commandMenu;
    public int PlayerMp { get; private set; }
    public int PlayerMaxMp { get; private set; }
    public bool IsGuarding { get; private set; }
    public string ActionMessage { get; private set; } = "";
    public bool HasCommandMenu => commandMenu != null;
    public int NormalAttackPower
    {
        get {
            var growth = LilyEnglish.Growth.GrowthService.Instance;
            return growth != null ? growth.SkillDamage(PlayerAttack) : PlayerAttack;
        }
    }

    /// <summary>同じ戦闘を異なるテンポで比較するための設定。進行中の攻撃には適用しない。</summary>
    public void ConfigureTiming(float travelSeconds, float turnPauseSeconds, float resultPauseSeconds)
    {
        spellTravelDuration = Mathf.Max(0f, travelSeconds);
        turnDelay = Mathf.Max(0f, turnPauseSeconds);
        resultDelay = Mathf.Max(0f, resultPauseSeconds);
    }

    private int maxHp;
    private int startingPlayerHp;
    private int goldReward;
    private bool isBoss;
    private bool isShowing;
    private Action<QuestEventResult> onBattleEnd;
    private QuestEventResult pendingResult;
    private Coroutine flashCoroutine;
    private Coroutine floatCoroutine;
    private string currentEnemyName = "敵";
    private const float EnemyLungeSeconds = 0.28f;
    private bool visualsCaptured;
    private Color enemyBaseColor;
    private Vector2 enemyBasePosition;
    private Vector3 enemyBaseScale = Vector3.one;
    private Sprite enemyBaseSprite;
    private Color floatBaseColor;
    private Vector2 floatBasePosition;
    private Color spellColor = new Color(1f, .78f, .32f);
    private QuestEmblemGraphic spellEffect;

    // 育成値の取得元は呼び出し側に置き、パネルは渡された能力値で戦う。
    public void Show(BattlePhase phase, string enemyName, QuestWordData word,
                     int hp, int enemyAttack, int playerHp, int playerMaxHp,
                     int playerAttack, int goldReward, Action<QuestEventResult> onBattleEnd)
    {
        CancelBattle();
        CaptureVisualDefaults();
        isShowing = true;
        // root が自身の場合、初回有効化による Awake で再び非表示にしない。
        if (root != null) root.SetActive(true);

        EnemyHp = Mathf.Max(0, hp);
        maxHp = Mathf.Max(1, hp);
        PlayerMaxHp = Mathf.Max(1, playerMaxHp);
        PlayerHp = Mathf.Clamp(playerHp, 0, PlayerMaxHp);
        startingPlayerHp = PlayerHp;
        PlayerAttack = Mathf.Max(1, playerAttack);
        EnemyAttack = Mathf.Max(1, enemyAttack);
        this.goldReward = Mathf.Max(0, goldReward);
        this.onBattleEnd = onBattleEnd;
        isBoss = phase == BattlePhase.Boss;
        currentEnemyName = string.IsNullOrEmpty(enemyName) ? "敵" : enemyName;
        LastSpellTravelSeconds = 0f;
        PlayerMaxMp = Mathf.Max(1, baseMaxMp);
        PlayerMp = PlayerMaxMp;
        IsGuarding = false;
        ActionMessage = "行動を選んでください";

        if (enemyNameText != null) enemyNameText.text = enemyName;
        SetSpellWord(word);
        if (englishText != null) englishText.gameObject.SetActive(UseLegacyQuiz);
        if (readingText != null) readingText.gameObject.SetActive(UseLegacyQuiz);
        if (bossLabel != null) bossLabel.SetActive(isBoss);
        if (enemyImage != null)
        {
            Sprite sprite = isBoss && bossSprite != null ? bossSprite : defaultEnemySprite;
            enemyImage.sprite = sprite != null ? sprite : enemyBaseSprite;
        }

        UpdateHpBar();
        if (resultBox != null) resultBox.SetActive(false);
        if (attackButton != null)
        {
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(OnAttackClicked);
            attackButton.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("BattlePanel: attackButton が未設定のため攻撃できません。");
        }

        if (presentation != null) presentation.Show(phase, word, this, attackButton);
        if (commandMenu != null) commandMenu.Bind(this);
        SetTurn(BattleTurnState.Entering);
        StartCoroutine(BeginBattle());
    }

    public void Hide()
    {
        CancelBattle();
        if (root != null) root.SetActive(false);
    }

    private IEnumerator BeginBattle()
    {
        if (PlayerHp <= 0)
        {
            yield return FinishBattle(false);
            yield break;
        }
        if (EnemyHp <= 0)
        {
            yield return FinishBattle(true);
            yield break;
        }
        if (isBoss) yield return BossEntrance();
        SetTurn(BattleTurnState.PlayerTurn);
    }

    private void OnAttackClicked()
    {
        // 旧回帰テストの直接攻撃経路も保持する。
        if (UseLegacyQuiz) {
            if (!isActiveAndEnabled || CurrentTurn != BattleTurnState.PlayerTurn) return;
            SetTurn(BattleTurnState.PlayerAttacking);
            ApplyAttack(false);
            return;
        }
        TryAttack();
    }

    public bool TryAttack() => TryExecute(BattleActions.Attack);
    public bool TryDefend() => TryExecute(BattleActions.Defend);
    public bool CanCastSpell(BattleSpell spell) => spell != null &&
        !UseLegacyQuiz && isActiveAndEnabled && CurrentTurn == BattleTurnState.PlayerTurn && PlayerMp >= spell.MpCost;
    public bool TryCastSpell(BattleSpell spell) => CanCastSpell(spell) && TryExecute(spell);

    private bool TryExecute(BattleAction action)
    {
        if (action == null || UseLegacyQuiz || !isActiveAndEnabled || CurrentTurn != BattleTurnState.PlayerTurn || PlayerMp < action.MpCost) return false;
        BattleActionResult effect = action.Resolve(NormalAttackPower);
        SfxDirector.Play(action is BattleSpell ? SfxDirector.Magic : effect.Guard ? SfxDirector.Guard : SfxDirector.Attack);
        PlayerMp = Mathf.Clamp(PlayerMp - action.MpCost + effect.MpRecovery, 0, PlayerMaxMp);
        IsGuarding = effect.Guard;
        ActionMessage = effect.Message;
        // 入力を同期的にロックし、同じフレームの別コマンドも二重実行しない。
        SetTurn(BattleTurnState.PlayerAttacking);
        if (effect.Damage > 0) DealDamage(effect.Damage, false);
        StartCoroutine(ResolveTurn());
        return true;
    }

    public void SetSpellWord(QuestWordData word)
    {
        if (englishText != null) englishText.text = word != null ? word.english : "???";
        if (readingText != null) readingText.text = word != null ? word.reading : "";
        switch (word != null ? word.english : "")
        {
            case "fire": spellColor = new Color(1f, .38f, .2f); break;
            case "water": spellColor = new Color(.3f, .8f, 1f); break;
            case "thunder": spellColor = new Color(1f, .9f, .25f); break;
            case "forest": spellColor = new Color(.4f, 1f, .6f); break;
            case "shadow": spellColor = new Color(.8f, .5f, 1f); break;
            default: spellColor = new Color(1f, .78f, .32f); break;
        }
    }

    /// <summary>一発正解は威力2倍。入力を即座にロックしてから魔法を飛ばす。</summary>
    public bool TryCastWordMagic(bool critical)
    {
        if (!UseLegacyQuiz || !isActiveAndEnabled || CurrentTurn != BattleTurnState.PlayerTurn) return false;
        SetTurn(BattleTurnState.PlayerAttacking);
        StartCoroutine(CastWordMagic(critical));
        return true;
    }

    private IEnumerator CastWordMagic(bool critical)
    {
        SfxDirector.Play(SfxDirector.Magic);
        float startedAt = Time.realtimeSinceStartup;
        float duration = spellTravelDuration;
        RectTransform origin = presentation != null ? presentation.MagicOrigin : null;
        if (origin == null && englishText != null) origin = englishText.rectTransform;
        if (origin != null && enemyImage != null && root != null)
        {
            if (spellEffect == null)
            {
                var effect = new GameObject("WordMagicEffect", typeof(RectTransform), typeof(CanvasRenderer));
                effect.transform.SetParent(root.transform, false);
                spellEffect = effect.AddComponent<QuestEmblemGraphic>();
                spellEffect.symbol = QuestEmblemGraphic.Symbol.Star;
                spellEffect.raycastTarget = false;
            }
            RectTransform rt = spellEffect.rectTransform;
            rt.SetAsLastSibling();
            rt.sizeDelta = Vector2.one * (critical ? 110f : 72f);
            spellEffect.lineWidth = critical ? 7f : 5f;
            spellEffect.color = spellColor;
            spellEffect.gameObject.SetActive(true);
            Vector3 start = origin.TransformPoint(origin.rect.center);
            Vector3 end = enemyImage.rectTransform.TransformPoint(enemyImage.rectTransform.rect.center);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                rt.position = Vector3.Lerp(start, end, t);
                rt.localRotation = Quaternion.Euler(0f, 0f, t * 160f);
                yield return null;
                elapsed = Time.realtimeSinceStartup - startedAt;
            }
            rt.position = end;
            spellEffect.gameObject.SetActive(false);
        }
        LastSpellTravelSeconds = Time.realtimeSinceStartup - startedAt;
        ApplyAttack(critical, true);
    }

    private void ApplyAttack(bool critical, bool wordMagic = false)
    {
        // 魔法は詠唱開始時に se_magic を鳴らし済み。通常攻撃だけここで se_attack
        if (!wordMagic) SfxDirector.Play(SfxDirector.Attack);
        // 冒険の攻撃は Lily の固有スキル倍率（例: スペルスター Lv1 = 攻撃力の200%）を掛ける
        int power = PlayerAttack;
        var growth = LilyEnglish.Growth.GrowthService.Instance;
        if ((wordMagic || !UseLegacyQuiz) && growth != null) power = growth.SkillDamage(PlayerAttack);
        // long で倍率を計算し、大きな育成値でもオーバーフローさせない。
        DealDamage((long)power * (critical ? 2 : 1), critical);
        StartCoroutine(ResolveTurn());
    }

    private void DealDamage(long power, bool critical)
    {
        int damage = (int)Math.Min(EnemyHp, Math.Max(0, power));
        EnemyHp -= damage;
        UpdateHpBar();
        PlayDamageFlash();
        ShowDamageNumber(damage, critical);
        if (TapAttackManager.Instance != null) TapAttackManager.Instance.PlayHitSound();
    }

    private IEnumerator ResolveTurn()
    {
        // 倒れた敵は反撃しない。決着後は即座に終端状態へ移る。
        if (EnemyHp <= 0)
        {
            yield return FinishBattle(true);
            yield break;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, turnDelay));
        SetTurn(BattleTurnState.EnemyTurn);
        // 50%軽減、奇数は切り上げ。今回の反撃を処理したら必ず解除する。
        bool guarded = IsGuarding;
        int incoming = guarded ? (int)(((long)EnemyAttack + 1) / 2) : EnemyAttack;
        int dealt = Mathf.Min(PlayerHp, incoming);
        ActionMessage = guarded ? $"{currentEnemyName}の攻撃！ ガードで軽減した" : $"{currentEnemyName}の攻撃！";
        if (commandMenu != null) commandMenu.Refresh();
        // 攻撃してきたことが見えるよう、敵が飛びかかる動きを見せてから HP を減らす
        yield return EnemyLunge();
        PlayerHp -= dealt;
        IsGuarding = false;
        ActionMessage = $"Lily は {dealt} ダメージを受けた！";
        if (commandMenu != null) commandMenu.Refresh();
        SfxDirector.Play(guarded ? SfxDirector.Guard : SfxDirector.Hit);
        if (presentation != null) presentation.PlayLilyHit(dealt, guarded);
        UpdateHpBar();

        if (PlayerHp <= 0)
        {
            yield return FinishBattle(false);
            yield break;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, turnDelay));
        SetTurn(BattleTurnState.PlayerTurn);
    }

    private void SetTurn(BattleTurnState turn)
    {
        CurrentTurn = turn;
        if (turn == BattleTurnState.PlayerTurn) ActionMessage = "行動を選んでください";
        if (turn == BattleTurnState.Hidden) { IsGuarding = false; ActionMessage = ""; }
        if (commandMenu != null) commandMenu.Refresh();
        if (attackButton != null) attackButton.interactable = turn == BattleTurnState.PlayerTurn;
        if (presentation != null) presentation.Refresh();
        if (attackButtonText == null) return;
        if (commandMenu != null && !UseLegacyQuiz) { attackButtonText.text = "攻撃"; return; }
        switch (turn)
        {
            case BattleTurnState.PlayerTurn:
                attackButtonText.text = isBoss ? "全力攻撃！" : "攻撃！";
                break;
            case BattleTurnState.PlayerAttacking: attackButtonText.text = "Lilyの攻撃！"; break;
            case BattleTurnState.EnemyTurn: attackButtonText.text = "敵の攻撃！"; break;
            case BattleTurnState.Entering: attackButtonText.text = "敵が現れた！"; break;
        }
    }

    private void UpdateHpBar()
    {
        if (hpSlider != null) hpSlider.value = (float)EnemyHp / maxHp;
        // 既存テキストを使い、シーンの再配置なしで双方の HP を確認できる。
        if (hpText != null) hpText.text = $"敵 {EnemyHp} / {maxHp}　Lily {PlayerHp} / {PlayerMaxHp}";
        if (presentation != null) presentation.Refresh();
    }

    private IEnumerator FinishBattle(bool victory)
    {
        SetTurn(victory ? BattleTurnState.Victory : BattleTurnState.Defeat);
        if (attackButton != null) attackButton.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, resultDelay));

        pendingResult = new QuestEventResult
        {
            success = victory,
            goldGained = victory ? goldReward : 0,
            expGained = victory ? 50 : 0,
            hpChange = PlayerHp - startingPlayerHp,
            message = victory ? $"敵を倒した！\n{goldReward}G 獲得！" : "Lilyは力尽きてしまった…"
        };
        if (resultText != null) resultText.text = pendingResult.message;
        if (resultBox != null) resultBox.SetActive(true);

        if (okButton != null && okButton.gameObject.activeInHierarchy)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(ConfirmResult);
            okButton.interactable = true;
        }
        else
        {
            Debug.LogWarning("BattlePanel: 利用できるOKボタンが無いため、そのまま進行します。");
            ConfirmResult();
        }
    }

    private void ConfirmResult()
    {
        if (pendingResult == null) return;
        QuestEventResult result = pendingResult;
        Action<QuestEventResult> callback = onBattleEnd;
        // 呼び出し先で Hide / Show されても、新しい戦闘へ古い結果を渡さない。
        pendingResult = null;
        onBattleEnd = null;
        if (okButton != null) okButton.interactable = false;
        if (resultBox != null) resultBox.SetActive(false);
        if (callback != null) callback(result);
    }

    private void PlayDamageFlash()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        RestoreEnemyColor();
        flashCoroutine = StartCoroutine(DamageFlash());
    }

    private IEnumerator DamageFlash()
    {
        if (enemyImage == null) yield break;
        enemyImage.color = damageColor;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, flashDuration));
        RestoreEnemyColor();
        flashCoroutine = null;
    }

    private void ShowDamageNumber(int damage, bool critical = false)
    {
        if (damageFloatText == null) return;
        if (floatCoroutine != null) StopCoroutine(floatCoroutine);
        ResetDamageNumber();
        damageFloatText.text = critical ? "<size=65%>CRITICAL</size>\n-" + damage : "-" + damage;
        damageFloatText.gameObject.SetActive(true);
        floatCoroutine = StartCoroutine(FloatAndFade());
    }

    private IEnumerator FloatAndFade()
    {
        float elapsed = 0f;
        const float duration = 0.8f;
        while (elapsed < duration && damageFloatText != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            damageFloatText.rectTransform.anchoredPosition = floatBasePosition + new Vector2(0f, progress * 60f);
            damageFloatText.color = new Color(floatBaseColor.r, floatBaseColor.g, floatBaseColor.b,
                                             floatBaseColor.a * (1f - progress));
            yield return null;
        }
        ResetDamageNumber();
        floatCoroutine = null;
    }

    /// <summary>敵が一瞬ふくらんで手前に踏み込み、元に戻る。ターンの進行には関与しない演出</summary>
    private IEnumerator EnemyLunge()
    {
        if (enemyImage == null) yield break;
        RectTransform rt = enemyImage.rectTransform;
        Vector2 from = enemyBasePosition;
        Vector2 to = enemyBasePosition + new Vector2(0f, -36f);
        Vector3 baseScale = enemyBaseScale;
        float half = EnemyLungeSeconds * 0.45f;
        float elapsed = 0f;
        while (elapsed < half && rt != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            rt.anchoredPosition = Vector2.Lerp(from, to, t);
            rt.localScale = Vector3.Lerp(baseScale, baseScale * 1.14f, t);
            yield return null;
        }
        elapsed = 0f;
        float back = EnemyLungeSeconds - half;
        while (elapsed < back && rt != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / back);
            rt.anchoredPosition = Vector2.Lerp(to, from, t);
            rt.localScale = Vector3.Lerp(baseScale * 1.14f, baseScale, t);
            yield return null;
        }
        if (rt != null) { rt.anchoredPosition = from; rt.localScale = baseScale; }
    }

    private IEnumerator BossEntrance()
    {
        if (enemyImage == null) yield break;
        RectTransform rt = enemyImage.rectTransform;
        Vector2 start = enemyBasePosition + new Vector2(400f, 0f);
        float elapsed = 0f;
        const float duration = 0.5f;
        while (elapsed < duration && rt != null)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(start, enemyBasePosition,
                                               Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        if (rt != null) rt.anchoredPosition = enemyBasePosition;
    }

    private void CaptureVisualDefaults()
    {
        if (visualsCaptured) return;
        if (enemyImage != null)
        {
            enemyBaseColor = enemyImage.color;
            enemyBasePosition = enemyImage.rectTransform.anchoredPosition;
            enemyBaseScale = enemyImage.rectTransform.localScale;
            enemyBaseSprite = enemyImage.sprite;
        }
        if (damageFloatText != null)
        {
            floatBaseColor = damageFloatText.color;
            floatBasePosition = damageFloatText.rectTransform.anchoredPosition;
        }
        visualsCaptured = true;
        ResetDamageNumber();
    }

    private void RestoreEnemyColor()
    {
        if (visualsCaptured && enemyImage != null) enemyImage.color = enemyBaseColor;
    }

    private void ResetDamageNumber()
    {
        if (!visualsCaptured || damageFloatText == null) return;
        damageFloatText.color = floatBaseColor;
        damageFloatText.rectTransform.anchoredPosition = floatBasePosition;
        damageFloatText.gameObject.SetActive(false);
    }

    private void CancelBattle()
    {
        StopAllCoroutines();
        if (spellEffect != null) spellEffect.gameObject.SetActive(false);
        flashCoroutine = null;
        floatCoroutine = null;
        pendingResult = null;
        onBattleEnd = null;
        isShowing = false;
        SetTurn(BattleTurnState.Hidden);
        if (attackButton != null) attackButton.onClick.RemoveListener(OnAttackClicked);
        if (okButton != null)
        {
            okButton.onClick.RemoveListener(ConfirmResult);
            okButton.interactable = false;
        }
        RestoreEnemyColor();
        if (visualsCaptured && enemyImage != null)
        {
            enemyImage.rectTransform.anchoredPosition = enemyBasePosition;
            enemyImage.rectTransform.localScale = enemyBaseScale;
        }
        ResetDamageNumber();
        if (resultBox != null) resultBox.SetActive(false);
    }

    private void OnDisable()
    {
        CancelBattle();
    }

    private void Awake()
    {
        if (!isShowing && root != null) root.SetActive(false);
    }
}
