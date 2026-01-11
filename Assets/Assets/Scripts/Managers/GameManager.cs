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

        private EnemyController currentEnemy; // 現在の敵

        [Header("Game Data")]
        public long currentGold = 0; // 現在の所持ゴールド
        public int currentScore = 0; // 倒した敵の数（スコア）

        [Header("Player & Environment")]
        public PlayerController playerController;
        public BackgroundScroller backgroundScroller;

        [Header("Status Window (ドラクエ風UI)")]
        public StatusWindowController statusWindow;

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

            // BackgroundScrollerが未設定の場合、自動的に探す
            if (backgroundScroller == null)
            {
                backgroundScroller = FindObjectOfType<BackgroundScroller>();
            }

            // StatusWindowControllerが未設定の場合、自動的に探す
            if (statusWindow == null)
            {
                statusWindow = FindObjectOfType<StatusWindowController>();
            }

            StartGame();
        }

        //The method is invoked by tapping the screen
        public void Click()
        {
            Debug.Log("GameManager: Click() called"); // ★ログ追加

            // 敵が存在する場合は敵を攻撃
            if (currentEnemy != null)
            {
                currentEnemy.OnTap();
                
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
            else
            {
                // 敵がいない場合は従来のクリック処理
                DataManager.data.Money += DataManager.data.MoneyByClick;
                StartCoroutine(CameraShake.Shake(Camera.main.transform, 0.1f, 0.01f));
                Managers.Instance.particleManager.PlayEffect(Managers.Instance.particleManager.clickEffect);
                Managers.Instance.uIManager.UpdateUI();
                Managers.Instance.audioManager.PlaySound(Managers.Instance.audioManager.Coin);
            }
        }

        //Method to start the game
        public void StartGame()
        {
            Managers.Instance.uIManager.ChangeScreen("GameScreen");
            StartCoroutine(MoneyPerSecond());

            // 最初の敵を生成
            SpawnEnemy();
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

            // WordManagerから単語データを取得
            if (WordManager.Instance == null)
            {
                Debug.LogError("WordManagerが見つかりません。");
                return;
            }

            WordData wordData = WordManager.Instance.GetRandomWord();
            if (wordData == null)
            {
                Debug.LogError("単語データの取得に失敗しました。");
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

            // 敵を初期化
            currentEnemy.Setup(wordData);

            // ★プレイヤーと背景を停止（遭遇）
            if (playerController != null) playerController.SetState(PlayerController.PlayerState.Idle);
            if (backgroundScroller != null) backgroundScroller.SetScrolling(false);

            Debug.Log($"★敵を生成しました: {wordData.english}");
            Debug.Log($"  currentEnemy: {currentEnemy} (InstanceID: {currentEnemy.GetInstanceID()})");
        }

        /// <summary>
        /// 敵を倒した時に報酬を追加する
        /// </summary>
        /// <param name="gold">獲得ゴールド</param>
        /// <param name="score">獲得スコア</param>
        public void AddReward(long gold, int score)
        {
            currentGold += gold;
            currentScore += score;

            // UI更新（バウンド演出付き）
            if (Managers.Instance != null && Managers.Instance.uIManager != null)
            {
                Managers.Instance.uIManager.UpdateGoldAndScore(currentGold, currentScore);
            }

            // ステータスウィンドウの更新
            if (statusWindow != null)
            {
                statusWindow.SetGold(currentGold);
                statusWindow.SetScore(currentScore);
            }

            Debug.Log($"報酬獲得: {gold} Gold, スコア: {score} (合計: {currentGold} Gold, {currentScore} Score)");
        }

        /// <summary>
        /// 敵が倒された時にEnemyControllerから呼ばれる
        /// </summary>
        public void OnEnemyDefeated(EnemyController enemy)
        {
            Debug.Log($"★OnEnemyDefeated呼び出し:");
            Debug.Log($"  currentEnemy: {currentEnemy} (InstanceID: {(currentEnemy != null ? currentEnemy.GetInstanceID().ToString() : "null")})");
            Debug.Log($"  enemy: {enemy} (InstanceID: {(enemy != null ? enemy.GetInstanceID().ToString() : "null")})");
            Debug.Log($"  一致: {(currentEnemy == enemy)}");

            if (currentEnemy == enemy)
            {
                currentEnemy = null;
                Debug.Log("★敵を倒した！次の敵を1秒後に生成します...");

                // ★次の敵まで走る（再出発）
                if (playerController != null) playerController.SetState(PlayerController.PlayerState.Running);
                if (backgroundScroller != null) backgroundScroller.SetScrolling(true);

                // 少し待ってから次の敵を生成
                StartCoroutine(SpawnNextEnemyAfterDelay(1.0f));
            }
            else
            {
                Debug.LogWarning("★currentEnemyとenemyが一致しません！次の敵は生成されません。");
                Debug.LogWarning("★強制的に次の敵を生成します（暫定対応）");

                // 暫定対応: 参照が一致しなくても次の敵を生成
                currentEnemy = null;
                if (playerController != null) playerController.SetState(PlayerController.PlayerState.Running);
                if (backgroundScroller != null) backgroundScroller.SetScrolling(true);
                StartCoroutine(SpawnNextEnemyAfterDelay(1.0f));
            }
        }

        /// <summary>
        /// 遅延後に次の敵を生成
        /// </summary>
        private IEnumerator SpawnNextEnemyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
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
            AdmobManager.instance.ShowBanner(); //An example of calling a banner from a script
           // AdmobManager.instance.HideBanner(); //Hide banner


            AdmobManager.instance.ShowInterstitial();// An example of calling a Interstitial from a script

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