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

    private int currentStep  = 0;
    private int totalSteps   = 3;
    private QuestDataSO activeData;

    // ─── クエスト開始 ────────────────────────────────────────────
    public void StartQuest(QuestDataSO data = null)
    {
        if (CurrentState != QuestState.Idle)
        {
            Debug.LogWarning($"[QuestManager] クエスト進行中のため開始を無視しました: {CurrentState}");
            return;
        }

        activeData   = data ?? questData ?? BuildDefaultData();
        totalSteps   = activeData.totalSteps;
        currentStep  = 0;
        PlayerMaxHp  = activeData.playerMaxHp;
        PlayerHp     = PlayerMaxHp;
        QuestGold    = 0;
        QuestExp     = 0;

        Debug.Log($"[QuestManager] クエスト開始: {activeData.questName}");
        pipHint?.ShowHint("クエストが始まった！\n左右どちらかの道を選んでね！");
        ShowChoice();
    }

    // ─── 選択画面 ────────────────────────────────────────────────
    private void ShowChoice()
    {
        CurrentState = QuestState.ShowingChoice;
        choicePanel?.Show(currentStep, totalSteps, OnChoiceSelected);
    }

    private void OnChoiceSelected(bool isLeft)
    {
        choicePanel?.Hide();
        QuestEventType ev = RollEventType();
        Debug.Log($"[Quest] ステップ{currentStep + 1}: {ev}");

        switch (ev)
        {
            case QuestEventType.Villager: StartVillagerEvent(); break;
            case QuestEventType.Chest:   StartChestEvent();    break;
            case QuestEventType.Enemy:   StartEnemyBattle();   break;
        }
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
        pipHint?.ShowHint("村人に出会った！\n話しかけてみよう。");
        villagerPanel?.Show(scenario, OnVillagerComplete);
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
        pipHint?.ShowHint("宝箱を発見！\n開けてみよう！");
        chestPanel?.Show(activeData, OnChestComplete);
    }

    // ─── 道中敵バトル ────────────────────────────────────────────
    private void StartEnemyBattle()
    {
        CurrentState = QuestState.EnemyBattle;
        QuestWordData word = QuestWordList.GetRandom();
        int hp = Mathf.Max(1, word.english.Length * activeData.enemyHpMultiplier);
        pipHint?.ShowHint($"敵が現れた！\n「{word.english}」の意味を思い出して戦え！");
        battlePanel?.Show(BattlePhase.MidPath,
                          "まものの " + word.english, word,
                          hp, activeData.enemyGoldReward,
                          OnEnemyBattleComplete);
    }

    // ─── ボスバトル ──────────────────────────────────────────────
    private void StartBossBattle()
    {
        CurrentState = QuestState.BossBattle;
        BossData boss = activeData.bossData ?? new BossData();
        QuestWordData word = QuestWordList.GetRandom();
        pipHint?.ShowHint($"ボス「{boss.bossName}」が現れた！\n全力で戦え！！", true);
        battlePanel?.Show(BattlePhase.Boss,
                          boss.bossName, word,
                          boss.bossHp, boss.bossGoldReward,
                          OnBossBattleComplete);
    }

    // ─── 完了コールバック ────────────────────────────────────────
    public void OnVillagerComplete(QuestEventResult result)
    {
        ApplyResult(result);
        villagerPanel?.Hide();
        MoveToNextStep();
    }

    public void OnChestComplete(QuestEventResult result)
    {
        ApplyResult(result);
        chestPanel?.Hide();
        MoveToNextStep();
    }

    public void OnEnemyBattleComplete(QuestEventResult result)
    {
        ApplyResult(result);
        battlePanel?.Hide();
        if (PlayerHp <= 0) { GameOver(); return; }
        MoveToNextStep();
    }

    public void OnBossBattleComplete(QuestEventResult result)
    {
        ApplyResult(result);
        battlePanel?.Hide();
        if (PlayerHp <= 0) { GameOver(); return; }
        ShowQuestClear();
    }

    // ─── 結果適用 ────────────────────────────────────────────────
    private void ApplyResult(QuestEventResult result)
    {
        if (result == null) return;
        QuestGold += result.goldGained;
        QuestExp  += result.expGained;
        PlayerHp   = Mathf.Clamp(PlayerHp + result.hpChange, 0, PlayerMaxHp);
    }

    // ─── ステップ進行 ────────────────────────────────────────────
    private void MoveToNextStep()
    {
        currentStep++;
        if (currentStep >= totalSteps)
            StartCoroutine(Delay(0.5f, StartBossBattle));
        else
            StartCoroutine(Delay(0.5f, ShowChoice));
    }

    // ─── クリア ──────────────────────────────────────────────────
    private void ShowQuestClear()
    {
        CurrentState = QuestState.QuestClear;

        int totalGold = QuestGold + activeData.clearGoldBonus;
        GrantGold(totalGold);

        clearPanel?.Show(activeData.questName, activeData.clearMessage,
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

        Idle.DataManager.data.Money += amount;
        Idle.DataManager.SaveData();

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

        Debug.Log($"[QuestManager] クエスト報酬 {amount}G を加算（所持金: {Idle.DataManager.data.Money}）");
    }

    /// <summary>クエストが終了（クリア／ゲームオーバー問わず）した時に発火する</summary>
    public event System.Action OnQuestFinished;

    private void OnQuestClosed()
    {
        CurrentState = QuestState.Idle;
        OnQuestFinished?.Invoke();
    }

    // ─── ゲームオーバー ──────────────────────────────────────────
    private void GameOver()
    {
        CurrentState = QuestState.GameOver;
        pipHint?.ShowHint("力尽きてしまった…\n次は頑張ろう！");
        StartCoroutine(Delay(1.5f, () => clearPanel?.ShowGameOver(OnQuestClosed)));
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
        d.enemyGoldReward  = 100;
        d.normalGoldMin    = 50;
        d.normalGoldMax    = 150;
        d.rareGoldMin      = 300;
        d.rareGoldMax      = 600;
        d.rareItemName     = "まほうのかぎ";
        d.bossData         = new BossData();
        d.clearGoldBonus   = 300;
        d.clearMessage     = "クエストクリア！\n見事な冒険だった！";
        d.playerMaxHp      = 100;
        return d;
    }
}
