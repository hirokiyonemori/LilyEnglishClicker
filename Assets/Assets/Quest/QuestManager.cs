using System.Collections;
using UnityEngine;

/// <summary>
/// クエストフロー全体を制御するマネージャー
/// StartQuest() を呼ぶだけで 二択×3回 → ボス → クリア まで進む
/// </summary>
public class QuestManager : MonoBehaviour
{
    // ─── Inspector 設定 ──────────────────────────────────────────
    [Header("クエストデータ（null ならデフォルト使用）")]
    public QuestDataSO questData;

    [Header("各パネルの参照")]
    public QuestChoicePanel  choicePanel;
    public VillagerEventPanel villagerPanel;
    public ChestEventPanel    chestPanel;
    public BattlePanel        battlePanel;
    public QuestClearPanel    clearPanel;
    public PipHintController  pipHint;

    // ─── Singleton ──────────────────────────────────────────────
    public static QuestManager Instance { get; private set; }
    public static bool IsQuestActive => Instance != null && Instance.CurrentState != QuestState.Idle;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─── ランタイム状態 ──────────────────────────────────────────
    public QuestState CurrentState { get; private set; } = QuestState.Idle;

    public int  PlayerHp    { get; private set; }
    public int  PlayerMaxHp { get; private set; }
    public int  QuestGold   { get; private set; }
    public int  QuestExp    { get; private set; }

    /// <summary>直近の分岐で左（森の小道）を選んだか。背景の出し分け用で、進行には使わない</summary>
    public bool LastPathWasLeft { get; private set; } = true;

    /// <summary>questData 未設定時に使う Lily の最大HP（BuildDefaultData と共通）</summary>
    public const int DefaultPlayerMaxHp = 100;

    /// <summary>次のクエスト開始時の Lily の最大HP。本編HUDがクエスト外でも同じ値を表示するために使う。</summary>
    /// <summary>クエスト開始時の Lily の最大HP。強化システムがあればそのレベルの HP、無ければクエスト設定値</summary>
    public int BasePlayerMaxHp
    {
        get
        {
            var growth = LilyEnglish.Growth.GrowthService.Instance;
            if (growth != null && growth.MainCharacter != null) return growth.MainMaxHp;
            if (activeData != null && CurrentState != QuestState.Idle) return Mathf.Max(1, activeData.playerMaxHp);
            return questData != null ? Mathf.Max(1, questData.playerMaxHp) : DefaultPlayerMaxHp;
        }
    }

    private int currentStep  = 0;
    private int totalSteps   = 3;
    private QuestDataSO activeData;
    private bool battleResultPending;
    private int battleSequence;

    // ─── クエスト開始 ────────────────────────────────────────────
    public void StartQuest(QuestDataSO data = null)
    {
        if (CurrentState != QuestState.Idle)
        {
            Debug.LogWarning($"[QuestManager] クエスト進行中のため開始を無視しました: {CurrentState}");
            return;
        }

        // ?? は Unity のオブジェクトに対して正しく動かない（未割り当て参照は
        // 「nullでない」と判定される）ため、明示的に != null で解決する
        activeData   = ResolveQuestData(data);
        totalSteps   = Mathf.Max(1, activeData.totalSteps);
        currentStep  = 0;
        PlayerMaxHp  = BasePlayerMaxHp;
        PlayerHp     = PlayerMaxHp;
        QuestGold    = 0;
        QuestExp     = 0;

        Debug.Log($"[QuestManager] クエスト開始: {activeData.questName}");
        ShowHint("クエストが始まった！\n左右どちらかの道を選んでね！");
        ShowChoice();
    }

    private QuestDataSO ResolveQuestData(QuestDataSO data)
    {
        if (data != null) return data;
        if (questData != null) return questData;
        return BuildDefaultData();
    }

    /// <summary>pipHint は任意なので、未設定でも落ちないようにする</summary>
    private void ShowHint(string message, bool excited = false)
    {
        if (pipHint != null) pipHint.ShowHint(message, excited);
    }

    // ─── 選択画面 ────────────────────────────────────────────────
    private void ShowChoice()
    {
        CurrentState = QuestState.ShowingChoice;

        if (choicePanel == null)
        {
            // 選択パネルが無いと進行不能になるため、抽選だけ行って先へ進める
            Debug.LogError("[QuestManager] choicePanel が未設定です。選択をスキップします。");
            OnChoiceSelected(true);
            return;
        }

        choicePanel.Show(currentStep, totalSteps, OnChoiceSelected);
    }

    private void OnChoiceSelected(bool isLeft)
    {
        LastPathWasLeft = isLeft;
        if (choicePanel != null) choicePanel.Hide();
        QuestEventType ev = RollEventType();
        Debug.Log($"[Quest] ステップ{currentStep + 1}: {ev}");

        switch (ev)
        {
            case QuestEventType.Villager: SafeRun(StartVillagerEvent, "村人イベント"); break;
            case QuestEventType.Chest:    SafeRun(StartChestEvent,    "宝箱イベント"); break;
            case QuestEventType.Enemy:    SafeRun(StartEnemyBattle,   "道中バトル");   break;
        }
    }

    /// <summary>
    /// イベント開始処理を安全に実行する。
    ///
    /// 途中で例外が出ると CurrentState が進行中のまま固定され、
    /// IsQuestActive が真のままになるため本編のタップまで効かなくなる
    /// （実際に ChestEventPanel の未割り当て参照でこの状態が発生した）。
    /// 失敗した場合はクエストを中断して本編へ制御を戻す。
    /// </summary>
    private void SafeRun(System.Action step, string label)
    {
        try
        {
            step();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QuestManager] {label} で例外が発生したためクエストを中断します。\n{e}");
            AbortQuest();
        }
    }

    /// <summary>クエストを強制終了し、本編を操作できる状態へ戻す</summary>
    public void AbortQuest()
    {
        StopAllCoroutines();

        if (choicePanel   != null) choicePanel.Hide();
        if (villagerPanel != null) villagerPanel.Hide();
        if (chestPanel    != null) chestPanel.Hide();
        if (battlePanel   != null) battlePanel.Hide();
        if (clearPanel    != null) clearPanel.Hide();

        OnQuestClosed();
    }

    // ─── イベント種別の重み付きランダム ─────────────────────────
    private QuestEventType RollEventType()
    {
        int total = activeData.villagerWeight + activeData.chestWeight + activeData.enemyWeight;
        int roll  = Random.Range(0, total);
        if (roll < activeData.villagerWeight) return QuestEventType.Villager;
        if (roll < activeData.villagerWeight + activeData.chestWeight) return QuestEventType.Chest;
        return QuestEventType.Enemy;
    }

    // ─── 村人イベント ────────────────────────────────────────────
    private void StartVillagerEvent()
    {
        CurrentState = QuestState.VillagerEvent;
        VillagerScenario scenario = PickVillagerScenario();
        ShowHint("村人に出会った！\n話しかけてみよう。");

        if (villagerPanel == null)
        {
            Debug.LogError("[QuestManager] villagerPanel が未設定です。イベントをスキップします。");
            OnVillagerComplete(null);
            return;
        }

        villagerPanel.Show(scenario, OnVillagerComplete);
    }

    private VillagerScenario PickVillagerScenario()
    {
        VillagerScenario[] pool = (activeData.villagerScenarios != null && activeData.villagerScenarios.Length > 0)
            ? activeData.villagerScenarios
            : DefaultVillagerScenarios.Get();
        return pool[Random.Range(0, pool.Length)];
    }

    // ─── 宝箱イベント ────────────────────────────────────────────
    private void StartChestEvent()
    {
        CurrentState = QuestState.ChestEvent;
        ShowHint("宝箱を発見！\n開けてみよう！");

        if (chestPanel == null)
        {
            Debug.LogError("[QuestManager] chestPanel が未設定です。イベントをスキップします。");
            OnChestComplete(null);
            return;
        }

        chestPanel.Show(activeData, OnChestComplete);
    }

    // ─── 道中敵バトル ────────────────────────────────────────────
    private void StartEnemyBattle()
    {
        QuestWordData word = null;
        int hp = Mathf.Max(1, 6 * activeData.enemyHpMultiplier);
        ShowHint("敵が現れた！\n行動を選んでLilyと戦おう！");

        StartBattle(BattlePhase.MidPath, "森のまもの", word,
                    hp, ScaledEnemyAttack(activeData.enemyAttack, activeData.enemyAttackPercent), activeData.enemyGoldReward);
    }

    // ─── ボスバトル ──────────────────────────────────────────────
    private void StartBossBattle()
    {
        BossData boss = activeData.bossData ?? new BossData();
        QuestWordData word = null;
        ShowHint($"ボス「{boss.bossName}」が現れた！\n全力で戦え！！", true);

        StartBattle(BattlePhase.Boss, boss.bossName, word,
                    boss.bossHp, ScaledEnemyAttack(boss.bossAttack, boss.bossAttackPercent), boss.bossGoldReward);
    }

    /// <summary>
    /// 敵の攻撃力。固定値と「Lily の最大HPに対する割合」の大きい方。
    /// 固定値（1〜2）だけだと、育成で HP が 100 を超えた Lily には傷にならず、敵が攻撃していないように見えた。
    /// </summary>
    private int ScaledEnemyAttack(int fixedAttack, int percentOfMaxHp)
    {
        int scaled = Mathf.CeilToInt(PlayerMaxHp * Mathf.Max(0, percentOfMaxHp) / 100f);
        return Mathf.Max(1, Mathf.Max(fixedAttack, scaled));
    }

    private void StartBattle(BattlePhase phase, string enemyName, QuestWordData word,
                             int hp, int enemyAttack, int goldReward)
    {
        if (PlayerHp <= 0) { GameOver(); return; }

        CurrentState = phase == BattlePhase.Boss ? QuestState.BossBattle : QuestState.EnemyBattle;
        battleResultPending = true;
        int sequence = ++battleSequence;
        System.Action<QuestEventResult> onBattleEnd = result =>
        {
            // 前の戦闘のコールバックが遅れて届いても、現在の戦闘に適用しない。
            if (sequence != battleSequence) return;
            CompleteBattle(phase, result);
        };

        if (battlePanel == null)
        {
            Debug.LogError("[QuestManager] battlePanel が未設定です。バトルをスキップします。");
            onBattleEnd(null);
            return;
        }

        battlePanel.Show(phase, enemyName, word,
                         Mathf.Max(1, hp), Mathf.Max(1, enemyAttack),
                         PlayerHp, PlayerMaxHp, GetPlayerAttack(), goldReward, onBattleEnd);
    }

    /// <summary>本編の育成済みタップ値を戦闘攻撃力へ変換する共通の取得口。</summary>
    public int GetPlayerAttack()
    {
        // Lily の攻撃力（強化システムの ATK を威力へ換算）。強化システムが無い時は従来どおり 1タップの値
        var growth = LilyEnglish.Growth.GrowthService.Instance;
        if (growth != null && growth.MainCharacter != null) return growth.AttackPower;

        if (Idle.DataManager.data == null) return 1;
        long attack = Idle.DataManager.data.MoneyByClick;
        if (attack <= 1) return 1;
        return attack >= int.MaxValue ? int.MaxValue : (int)attack;
    }

    // ─── 完了コールバック ────────────────────────────────────────
    public void OnVillagerComplete(QuestEventResult result)
    {
        ApplyResult(result);
        if (villagerPanel != null) villagerPanel.Hide();
        MoveToNextStep();
    }

    public void OnChestComplete(QuestEventResult result)
    {
        ApplyResult(result);
        if (chestPanel != null) chestPanel.Hide();
        MoveToNextStep();
    }

    public void OnEnemyBattleComplete(QuestEventResult result)
    {
        CompleteBattle(BattlePhase.MidPath, result);
    }

    public void OnBossBattleComplete(QuestEventResult result)
    {
        CompleteBattle(BattlePhase.Boss, result);
    }

    private void CompleteBattle(BattlePhase phase, QuestEventResult result)
    {
        QuestState expectedState = phase == BattlePhase.Boss ? QuestState.BossBattle : QuestState.EnemyBattle;
        if (CurrentState != expectedState || !battleResultPending) return;

        // OK の連打や遅延中の再通知で、HP・報酬・ステップを重複更新しない。
        battleResultPending = false;
        bool failed = result != null && !result.success;
        ApplyResult(result, !failed);
        if (battlePanel != null) battlePanel.Hide();
        if (failed || PlayerHp <= 0) { GameOver(); return; }

        if (phase == BattlePhase.Boss) ShowQuestClear();
        else MoveToNextStep();
    }

    // ─── 結果適用 ────────────────────────────────────────────────
    private void ApplyResult(QuestEventResult result, bool grantRewards = true)
    {
        if (result == null) return;
        if (grantRewards)
        {
            QuestGold += result.goldGained;
            QuestExp  += result.expGained;
        }
        PlayerHp   = Mathf.Clamp(PlayerHp + result.hpChange, 0, PlayerMaxHp);
    }

    // ─── ステップ進行 ────────────────────────────────────────────
    private void MoveToNextStep()
    {
        if (PlayerHp <= 0) { GameOver(); return; }

        currentStep++;
        if (currentStep >= totalSteps)
            StartCoroutine(Delay(0.5f, () => SafeRun(StartBossBattle, "ボス戦")));
        else
            StartCoroutine(Delay(0.5f, () => SafeRun(ShowChoice, "分岐選択")));
    }

    // ─── クリア ──────────────────────────────────────────────────
    private void ShowQuestClear()
    {
        CurrentState = QuestState.QuestClear;
        QuestEntryController.ResetAfterClear();

        int totalGold = QuestGold + activeData.clearGoldBonus;
        GrantGold(totalGold);

        // クエストで得た EXP を強化システムへ（以前は結果画面に出すだけで保存されていなかった）
        var growth = LilyEnglish.Growth.GrowthService.Instance;
        if (growth != null && QuestExp > 0) growth.AddCurrency(LilyEnglish.Growth.Currency.Exp, QuestExp);

        if (clearPanel == null)
        {
            // クリアパネルが無いと Idle に戻れず本編が操作不能になるため即座に閉じる
            Debug.LogError("[QuestManager] clearPanel が未設定です。クエストをそのまま終了します。");
            OnQuestClosed();
            return;
        }

        clearPanel.Show(activeData.questName, activeData.clearMessage,
                        totalGold, QuestExp, OnQuestClosed);
    }

    /// <summary>
    /// クエスト報酬を本編の所持金へ加算する。
    ///
    /// このクラスはグローバル名前空間にあるため、単に GameManager と書くと
    /// Assets/Scripts/ 側の旧プロトタイプ（シーンに存在しない）に解決されてしまい、
    /// 報酬が無言で捨てられていた。本編側は Idle 名前空間なので明示的に修飾する。
    /// </summary>
    private void GrantGold(int amount)
    {
        if (amount <= 0) return;

        if (Idle.DataManager.data == null)
        {
            Debug.LogWarning($"[QuestManager] DataManagerが未初期化のため報酬 {amount}G を反映できません。");
            return;
        }

        // 所持金への加算は GameManager.AddReward が行う（以前はここでも足していたため、
        // AddReward 側で所持金へ入れるようにした後は二重加算になる）
        if (Idle.Managers.Instance == null || Idle.Managers.Instance.gameManager == null)
            Idle.DataManager.data.Money += amount;
        if (Idle.Managers.Instance != null)
        {
            if (Idle.Managers.Instance.gameManager != null)
            {
                Idle.Managers.Instance.gameManager.AddReward(amount, 0);
            }

            if (Idle.Managers.Instance.uIManager != null)
            {
                Idle.Managers.Instance.uIManager.UpdateUI();
            }
        }

        Idle.DataManager.SaveData();
        Debug.Log($"[QuestManager] クエスト報酬 {amount}G を加算（所持金: {Idle.DataManager.data.Money}）");
    }

    /// <summary>クエストが終了（クリア／ゲームオーバー問わず）した時に発火する</summary>
    public event System.Action OnQuestFinished;

    private void OnQuestClosed()
    {
        battleResultPending = false;
        battleSequence++;
        CurrentState = QuestState.Idle;
        OnQuestFinished?.Invoke();
    }

    // ─── ゲームオーバー ──────────────────────────────────────────
    private void GameOver()
    {
        CurrentState = QuestState.GameOver;
        ShowHint("力尽きてしまった…\n次は頑張ろう！");
        StartCoroutine(Delay(1.5f, () =>
        {
            if (clearPanel != null)
            {
                clearPanel.ShowGameOver(OnQuestClosed);
            }
            else
            {
                Debug.LogError("[QuestManager] clearPanel が未設定です。クエストをそのまま終了します。");
                OnQuestClosed();
            }
        }));
    }

    // ─── ユーティリティ ──────────────────────────────────────────
    private IEnumerator Delay(float sec, System.Action action)
    {
        yield return new WaitForSeconds(sec);
        action?.Invoke();
    }

    private static QuestDataSO BuildDefaultData()
    {
        var d = ScriptableObject.CreateInstance<QuestDataSO>();
        d.questName        = "はじめての冒険";
        d.totalSteps       = 3;
        d.villagerWeight   = 33;
        d.chestWeight      = 33;
        d.enemyWeight      = 34;
        d.enemyHpMultiplier = 2;
        d.enemyAttack      = 1;
        d.enemyGoldReward  = 100;
        d.normalGoldMin    = 50;
        d.normalGoldMax    = 150;
        d.rareGoldMin      = 300;
        d.rareGoldMax      = 600;
        d.rareItemName     = "まほうのかぎ";
        d.bossData         = new BossData();
        d.clearGoldBonus   = 300;
        d.clearMessage     = "クエストクリア！\n見事な冒険だった！";
        d.playerMaxHp      = DefaultPlayerMaxHp;
        return d;
    }
}
