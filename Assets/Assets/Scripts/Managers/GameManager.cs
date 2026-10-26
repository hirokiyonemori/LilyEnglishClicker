using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Idle
{
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Enemy System")]
        public GameObject enemyPrefab; // 敵のプレハブ（Inspectorで設定）
        public Transform enemySpawnPoint; // 敵を生成する位置（Inspectorで設定）
        public EnemyDatabase enemyDatabase; // 出現する敵の種類一覧（未設定でも従来どおり動作する）

        private EnemyController currentEnemy; // 現在の敵

        /// <summary>本編で敵を1体倒すたびに発火する（冒険クエストの解放判定などが購読する）</summary>
        public static event System.Action EnemyDefeated;

        [Header("Game Data")]
        public long currentGold = 0; // 現在の所持ゴールド
        public int currentScore = 0; // 倒した敵の数（スコア）

        [Header("Player & Environment")]
        public PlayerController playerController;

        [Header("Status Window (ドラクエ風UI)")]
        public StatusWindowController statusWindow;

        [Header("Result Screen")]
        public ResultManager resultManager; // リザルト画面マネージャー
        public int targetScore = 25; // クリアに必要なスコア（敵の数）

        [Header("ステージ")]
        public StageDirector stageDirector; // 未設定なら自動検出。ある時は「拠点→ステージ選択→戦闘→ボス→クリア→拠点」の流れで動く

        // ステージ戦闘の状態（stageDirector がある時だけ使う）
        private bool bossPhase;                // 通常の敵を倒し終えて、ボスと戦っている
        private EnemyController bossEnemy;
        private Coroutine spawnRoutine;
        private Coroutine clearRoutine;

        public bool BossPhase => bossPhase;
        public MainBattleHealth MainHealth { get; } = new MainBattleHealth();
        [SerializeField, Min(.1f)] private float enemyAttackInterval = 4f;
        [Header("本編の自動攻撃")]
        [SerializeField, Min(.1f)] private float autoAttackInterval = 1.5f;
        [SerializeField, Range(.1f, 1f)] private float autoAttackPower = .75f;
        private float autoAttackElapsed;
        private bool appPaused;
        private DailyEnglishBonusPanel dailyPanel;
        private QuestEntryController questEntry;

        private bool BattleInputBlocked
        {
            get
            {
                if (global::QuestManager.IsQuestActive || appPaused || !Application.isFocused) return true;
                var growthUi = LilyEnglish.Growth.UI.GrowthUIView.Find();
                if (growthUi != null && growthUi.IsOpen) return true;
                if (dailyPanel == null) dailyPanel = FindAnyObjectByType<DailyEnglishBonusPanel>(FindObjectsInactive.Include);
                if (questEntry == null) questEntry = FindAnyObjectByType<QuestEntryController>(FindObjectsInactive.Include);
                return (dailyPanel != null && dailyPanel.panelRoot != null && dailyPanel.panelRoot.activeInHierarchy)
                    || (questEntry != null && questEntry.IsDialogOpen)
                    || (Managers.Instance != null && Managers.Instance.uIManager != null && Managers.Instance.uIManager.IsPaused);
            }
        }
        private void OnApplicationPause(bool paused) { appPaused = paused; autoAttackElapsed = 0; MainHealth.ResetAttackTimer(); }
        private void Update()
        {
            bool canAttack = stageDirector != null && stageDirector.IsBattling && !BattleInputBlocked
                && currentEnemy != null && currentEnemy.GetCurrentHp() > 0;
            if (!canAttack || !MainHealth.Active || MainHealth.IsDefeated) autoAttackElapsed = 0;
            else
            {
                autoAttackElapsed += Time.deltaTime;
                if (autoAttackElapsed >= autoAttackInterval)
                {
                    autoAttackElapsed = 0; // 復帰時にまとめて攻撃しない。
                    AttackEnemy(autoAttackPower);
                    canAttack = currentEnemy != null && currentEnemy.GetCurrentHp() > 0;
                }
            }
            int previousHp = MainHealth.Hp;
            if (!MainHealth.Tick(Time.deltaTime, canAttack, currentEnemy != null ? currentEnemy.AttackInterval(enemyAttackInterval) : enemyAttackInterval, currentEnemy != null ? currentEnemy.AttackDamage : 1)) return;
            SfxDirector.Play(SfxDirector.Hit);
            if (statusWindow != null) { statusWindow.SetHP(MainHealth.Hp, MainHealth.MaxHp); statusWindow.OnPlayerDamage(); }
            if (playerController != null)
            {
                var feedback = playerController.GetComponent<MainHitFeedback>();
                if (feedback == null) feedback = playerController.gameObject.AddComponent<MainHitFeedback>();
                feedback.Show(currentEnemy, previousHp - MainHealth.Hp);
            }
            if (MainHealth.IsDefeated) stageDirector.OnPlayerDefeated();
        }

        private void Start()
        {
            // PlayerControllerが未設定の場合、自動的に探す
            if (playerController == null)
            {
                playerController = FindObjectOfType<PlayerController>();
                if (playerController != null)
                {
                    Debug.Log("GameManager: PlayerControllerを自動検出しました");
                }
                else
                {
                    Debug.LogWarning("GameManager: PlayerControllerが見つかりません。Inspectorで設定するか、シーンに配置してください。");
                }
            }

            // StatusWindowControllerが未設定の場合、自動的に探す
            if (statusWindow == null)
            {
                statusWindow = FindObjectOfType<StatusWindowController>();
            }

            // ResultManagerが未設定の場合、自動的に探す
            if (resultManager == null)
            {
                resultManager = FindObjectOfType<ResultManager>();
                if (resultManager != null)
                {
                    Debug.Log("GameManager: ResultManagerを自動検出しました");
                }
            }

            if (stageDirector == null)
            {
                stageDirector = FindObjectOfType<StageDirector>();
            }

            StartGame();
        }

        //The method is invoked by tapping the screen
        public void Click()
        {
            if (MainHealth.IsDefeated || BattleInputBlocked) return;
            if (global::QuestManager.IsQuestActive)
            {
                Debug.Log("GameManager: クエスト進行中のため本編クリックを無視しました。");
                return;
            }

            // 拠点・クリア画面にいる間は攻撃もゴールド加算もしない
            if (stageDirector != null && !stageDirector.IsBattling) return;

            Debug.Log("GameManager: Click() called"); // ★ログ追加

            // 敵が存在する場合は敵を攻撃
            if (currentEnemy != null)
            {
                AttackEnemy(1f);
            }
            else
            {
                // 敵がいない場合は従来のクリック処理
                DataManager.data.Money += DataManager.data.MoneyByClick;
                StartCoroutine(CameraShake.Shake(Camera.main.transform, 0.1f, 0.01f));
                Managers.Instance.uIManager.UpdateUI();
                Managers.Instance.audioManager.PlaySound(Managers.Instance.audioManager.Coin);
            }
        }

        private void AttackEnemy(float power)
        {
            string tapSound = SfxDirector.Attack;
            if (playerController != null && currentEnemy.GetCurrentHp() > 0)
            {
                var vfx = playerController.GetComponent<MainAttackVfx>();
                if (vfx == null) vfx = playerController.gameObject.AddComponent<MainAttackVfx>();
                vfx.Play(currentEnemy);
                tapSound = vfx.AttackSound;
            }
            currentEnemy.OnPlayerAttack(tapSound, power, power < 1f);

            // プレイヤーの攻撃アニメーション
            if (playerController != null)
            {
                Debug.Log("GameManager: Calling playerController.Attack()"); // ★ログ追加
                playerController.Attack();
            }
            else
            {
                Debug.LogError("GameManager: playerController is NULL!"); // ★エラーログ追加
            }

            // ステータスウィンドウの顔を攻撃表情に
            if (statusWindow != null)
            {
                statusWindow.OnPlayerAttack();
            }
        }

        //Method to start the game
        public void StartGame()
        {
            Managers.Instance.uIManager.ChangeScreen("GameScreen");
            StartCoroutine(MoneyPerSecond());

            // 最初の敵を生成。ステージ制のときは拠点から始まり、ステージを選ぶまで敵は出さない
            if (stageDirector == null) SpawnEnemy();
        }

        /// <summary>
        /// 選んだステージを最初から始める（StageDirector から呼ばれる）。
        /// 1ステージ分の集計（currentGold / currentScore）だけを戻し、所持金・EXPなど保存データには触れない
        /// </summary>
        public void BeginStage()
        {
            EndBattle();
            currentGold = 0;
            currentScore = 0;
            bossPhase = false;
            var growth = LilyEnglish.Growth.GrowthService.Instance;
            MainHealth.Begin(growth != null && growth.MainCharacter != null ? growth.MainMaxHp : global::QuestManager.DefaultPlayerMaxHp);
            RefreshScoreUi();
            SpawnEnemy();
        }

        /// <summary>
        /// 戦闘をやめる（拠点へ戻る・クリア後）。敵と予約中の生成を片付ける。
        /// 飛んでいる最中のコインは所持金へ届くので、ゴールドは失われない
        /// </summary>
        public void EndBattle()
        {
            autoAttackElapsed = 0;
            MainHealth.Stop();
            if (playerController != null) playerController.GetComponent<MainAttackVfx>()?.Clear();
            if (playerController != null) playerController.GetComponent<MainHitFeedback>()?.Clear();
            if (spawnRoutine != null) { StopCoroutine(spawnRoutine); spawnRoutine = null; }
            if (clearRoutine != null) { StopCoroutine(clearRoutine); clearRoutine = null; }
            if (currentEnemy != null) Destroy(currentEnemy.gameObject);
            currentEnemy = null;
            bossEnemy = null;
            bossPhase = false;
            if (playerController != null) playerController.SetState(PlayerController.PlayerState.Idle);
        }

        private void RefreshScoreUi()
        {
            if (Managers.Instance != null && Managers.Instance.uIManager != null)
            {
                long wallet = DataManager.data != null ? DataManager.data.Money : currentGold;
                Managers.Instance.uIManager.UpdateGoldAndScore(wallet, currentScore);
            }
            if (statusWindow != null)
            {
                statusWindow.SetGold(currentGold);
                statusWindow.SetScore(currentScore);
            }
        }

        /// <summary>
        /// 新しい敵を生成する
        /// </summary>
        public void SpawnEnemy()
        {
            // 既に敵が存在する場合は生成しない
            if (currentEnemy != null)
            {
                Debug.LogWarning("★既に敵が存在するため、新しい敵は生成しません。");
                return;
            }

            // 敵プレハブが設定されているか確認
            if (enemyPrefab == null)
            {
                Debug.LogError("enemyPrefabが設定されていません。Inspectorで設定してください。");
                return;
            }

            // 生成位置の設定（設定されていなければCanvas内に生成）
            Transform spawnTransform = enemySpawnPoint != null ? enemySpawnPoint : transform;

            // 敵を生成
            GameObject enemyObj = Instantiate(enemyPrefab, spawnTransform);
            currentEnemy = enemyObj.GetComponent<EnemyController>();

            if (currentEnemy == null)
            {
                Debug.LogError("生成したオブジェクトにEnemyControllerコンポーネントがありません。");
                Destroy(enemyObj);
                return;
            }

            // 進行度に応じた敵の種類を抽選（未設定ならプレハブ既定の見た目のまま）
            EnemyData enemyData = PickEnemyData();

            // 敵を初期化
            currentEnemy.Setup(null, enemyData);
            autoAttackElapsed = 0;
            MainHealth.ResetAttackTimer();
            if (bossPhase) bossEnemy = currentEnemy;

            // ★プレイヤーを停止（遭遇）
            if (playerController != null) playerController.SetState(PlayerController.PlayerState.Idle);

            Debug.Log($"敵を生成しました: {(enemyData != null ? enemyData.displayName : "まもの")}");
            Debug.Log($"  currentEnemy: {currentEnemy} (InstanceID: {currentEnemy.GetInstanceID()})");
        }

        /// <summary>
        /// ステージ制では、遊んでいるステージのティア範囲から（25体を倒したらボス）。
        /// ステージ制でなければ従来どおり、進行度で全体から抽選する
        /// </summary>
        private EnemyData PickEnemyData()
        {
            if (enemyDatabase == null) return null;
            StageInfo stage = stageDirector != null ? StageProgress.ActiveInfo : null;
            if (stage == null) return enemyDatabase.GetRandomForProgress(currentScore, targetScore);
            return bossPhase
                ? enemyDatabase.GetBoss()
                : enemyDatabase.GetNormalForStage(currentScore, targetScore, stage.minTier, stage.maxTier);
        }

        /// <summary>
        /// 敵を倒した時に報酬を追加する
        /// </summary>
        /// <param name="gold">獲得ゴールド</param>
        /// <param name="score">獲得スコア</param>
        public void AddReward(long gold, int score)
        {
            // ボス撃破はスコア（通常の敵の撃破数）に数えない。クリア判定は OnEnemyDefeated が行う
            if (bossPhase) score = 0;

            currentGold += gold;   // この回（ステージ）の獲得額。リザルト表示用
            currentScore += score;

            // 所持金（セーブ対象）にも入れる。強化ショップの★はこの所持金で支払う
            if (gold > 0 && DataManager.data != null)
            {
                DataManager.data.Money += gold;
                // コイン1枚ごとの同期保存を避け、既存の毎秒保存でまとめて確定する。
            }

            // UI更新（バウンド演出付き）。上部の GOLD は所持金を表示する
            if (Managers.Instance != null && Managers.Instance.uIManager != null)
            {
                long wallet = DataManager.data != null ? DataManager.data.Money : currentGold;
                Managers.Instance.uIManager.UpdateGoldAndScore(wallet, currentScore);
            }

            // ステータスウィンドウの更新
            if (statusWindow != null)
            {
                statusWindow.SetGold(currentGold);
                statusWindow.SetScore(currentScore);
            }

            Debug.Log($"報酬獲得: {gold} Gold, スコア: {score} (合計: {currentGold} Gold, {currentScore} Score)");

            // ★クリア判定：目標スコアに到達したらリザルト表示
            CheckGameClear();
        }

        /// <summary>
        /// ゲームクリア判定
        /// </summary>
        private void CheckGameClear()
        {
            // ステージ制では、ボスを倒した時（OnEnemyDefeated）にクリアする
            if (stageDirector != null) return;

            if (currentScore >= targetScore)
            {
                Debug.Log($"★ゲームクリア！スコア: {currentScore}/{targetScore}");

                // リザルト画面を表示
                if (resultManager != null)
                {
                    resultManager.ShowResult(currentGold, currentScore, targetScore);

                    // プレイヤーを停止
                    if (playerController != null) playerController.SetState(PlayerController.PlayerState.Idle);
                }
                else
                {
                    Debug.LogWarning("GameManager: ResultManager が見つかりません。リザルト画面を表示できません。");
                }
            }
        }

        /// <summary>
        /// ボス撃破後：飛んでいるコインが所持金へ届くのを待ってから、クリア画面を出す。
        /// ゴールドは AddReward が所持金へ加算済みなので、ここでは加算せず表示するだけ（二重加算しない）
        /// </summary>
        private IEnumerator StageClearRoutine()
        {
            // コインは時間差で生成されるので、まず少し待ってから残りを見る
            yield return new WaitForSeconds(0.9f);
            float waited = 0f;
            while (waited < 4f && FindObjectsByType<Coin>(FindObjectsSortMode.None).Length > 0)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);

            clearRoutine = null;
            if (stageDirector != null) stageDirector.OnStageCleared(currentGold);
        }

        /// <summary>
        /// 敵が倒された時にEnemyControllerから呼ばれる
        /// </summary>
        public void OnEnemyDefeated(EnemyController enemy)
        {
            if (MainHealth.IsDefeated) return;
            EnemyDefeated?.Invoke();

            Debug.Log($"★OnEnemyDefeated呼び出し:");
            Debug.Log($"  currentEnemy: {currentEnemy} (InstanceID: {(currentEnemy != null ? currentEnemy.GetInstanceID().ToString() : "null")})");
            Debug.Log($"  enemy: {enemy} (InstanceID: {(enemy != null ? enemy.GetInstanceID().ToString() : "null")})");
            Debug.Log($"  一致: {(currentEnemy == enemy)}");

            // ステージ制：ボスを倒したらステージクリア（次の敵は出さない）
            if (stageDirector != null && bossEnemy != null && enemy == bossEnemy)
            {
                currentEnemy = null;
                bossEnemy = null;
                bossPhase = false;
                if (playerController != null) playerController.SetState(PlayerController.PlayerState.Idle);
                stageDirector.OnBossDefeated();
                clearRoutine = StartCoroutine(StageClearRoutine());
                return;
            }

            if (currentEnemy == enemy)
            {
                currentEnemy = null;
                Debug.Log("★敵を倒した！次の敵を1秒後に生成します...");

                // ★次の敵まで走る（再出発）
                if (playerController != null) playerController.SetState(PlayerController.PlayerState.Running);
                // 背景のフェードは自動継続（何もしない）

                // 少し待ってから次の敵を生成
                spawnRoutine = StartCoroutine(SpawnNextEnemyAfterDelay(1.0f));
            }
            else
            {
                Debug.LogWarning("★currentEnemyとenemyが一致しません！次の敵は生成されません。");
                Debug.LogWarning("★強制的に次の敵を生成します（暫定対応）");

                // 暫定対応: 参照が一致しなくても次の敵を生成
                currentEnemy = null;
                if (playerController != null) playerController.SetState(PlayerController.PlayerState.Running);
                // 背景のフェードは自動継続（何もしない）
                spawnRoutine = StartCoroutine(SpawnNextEnemyAfterDelay(1.0f));
            }
        }

        /// <summary>
        /// 遅延後に次の敵を生成
        /// </summary>
        private IEnumerator SpawnNextEnemyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            spawnRoutine = null;

            if (stageDirector != null)
            {
                // ステージ制：拠点へ戻った後は何も出さない。通常の敵を倒し終えたらボスを出す
                if (!stageDirector.IsBattling) yield break;
                if (!bossPhase && currentScore >= targetScore)
                {
                    bossPhase = true;
                    stageDirector.OnBossAppear();
                }
                SpawnEnemy();
                yield break;
            }

            // ★クリア済みの場合は敵を生成しない
            if (currentScore >= targetScore)
            {
                Debug.Log("★ゲームクリア済みのため、次の敵は生成しません。");
                yield break; // コルーチンを終了
            }

            SpawnEnemy();
        }
        //Method to pause
        public void Pause()
        {
            Managers.Instance.uIManager.ChangeScreen("PauseMenu");
        }

        //Mthhod call ads from AdmobManager
        public void ShowRewardAd()
        {
            // リワード広告だけを出す（以前は、バナー・インタースティシャルまで一度に出すサンプルだった）
            if (AdmobManager.instance == null) return;
            AdmobManager.instance.rewardCount = 10; //Set reward value
            AdmobManager.instance.ShowReward(); //Call the video
        }

        //The loop of adding money per second
        IEnumerator MoneyPerSecond()
        {
            yield return new WaitForSeconds(1); //WaitForSeconds(HERE HOW MANY SECONDS WAIT)

            DataManager.data.Money += DataManager.data.MoneyPerSecond;  //add money by second
            Managers.Instance.uIManager.UpdateUI();  //Updating UI
            DataManager.SaveData();  //Save data
            StartCoroutine(MoneyPerSecond());   //Repeat loop
        }
    }
}
