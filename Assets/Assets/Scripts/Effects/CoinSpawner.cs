using UnityEngine;
using System.Collections;

namespace Idle
{
    /// <summary>
    /// コインエクスプロージョンを管理するクラス
    /// 敵を倒した時に、複数のコインを四方八方に飛び散らせる
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        [Header("コイン設定")]
        [SerializeField] private GameObject coinPrefab; // コインのPrefab
        [SerializeField] private Transform coinUITarget; // コインが飛んでいく先（UIのゴールド表示）

        [Header("エクスプロージョン設定")]
        [SerializeField] private int minCoinCount = 10; // 最小コイン数
        [SerializeField] private int maxCoinCount = 20; // 最大コイン数
        [SerializeField] private float explosionRadius = 2f; // 爆発半径
        [SerializeField] private float minForce = 3f; // 最小の力
        [SerializeField] private float maxForce = 8f; // 最大の力
        [SerializeField] private float upwardBias = 1.5f; // 上方向のバイアス（噴水のように）
        [SerializeField] private float coinSpawnInterval = 0.05f; // コイン生成間隔（時間差演出）

        [Header("サウンド")]
        [SerializeField] private bool playBurstSound = true; // コイン飛び散りSEを再生するか

        private static CoinSpawner instance;
        public static CoinSpawner Instance => instance;

        private void Awake()
        {
            // シングルトン化（簡易版）
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Debug.LogWarning("CoinSpawner: 複数のインスタンスが存在します。このインスタンスを破棄します。");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // UIターゲットが未設定の場合、自動検出を試みる
            if (coinUITarget == null)
            {
                AutoDetectUITarget();
            }
        }

        /// <summary>
        /// UIのゴールド表示を自動検出
        /// </summary>
        private void AutoDetectUITarget()
        {
            // UIManager の GoldText を探す
            if (Managers.Instance != null && Managers.Instance.uIManager != null)
            {
                var goldText = Managers.Instance.uIManager.GoldText;
                if (goldText != null)
                {
                    coinUITarget = goldText.transform;
                    Debug.Log("CoinSpawner: UIターゲットを自動検出しました - " + goldText.name);
                }
                else
                {
                    Debug.LogWarning("CoinSpawner: GoldText が見つかりません。Inspectorで手動設定してください。");
                }
            }
        }

        /// <summary>
        /// コインエクスプロージョンを発生させる
        /// </summary>
        /// <param name="position">爆発位置（ワールド座標）</param>
        /// <param name="totalGold">総ゴールド報酬（コインの価値の合計）</param>
        public void SpawnCoinExplosion(Vector3 position, long totalGold)
        {
            if (coinPrefab == null)
            {
                Debug.LogError("CoinSpawner: coinPrefab が設定されていません！");
                return;
            }

            if (coinUITarget == null)
            {
                Debug.LogWarning("CoinSpawner: coinUITarget が設定されていません。自動検出を試みます。");
                AutoDetectUITarget();

                if (coinUITarget == null)
                {
                    Debug.LogError("CoinSpawner: UIターゲットが見つかりません。コイン演出をスキップします。");
                    return;
                }
            }

            // コイン数を決定
            int coinCount = Random.Range(minCoinCount, maxCoinCount + 1);

            // 各コインの価値（総額をコイン数で割る）
            long goldPerCoin = totalGold / coinCount;

            // コルーチンで時間差生成
            StartCoroutine(SpawnCoinsWithDelay(position, coinCount, goldPerCoin));

            // コイン飛び散りSEを再生
            if (playBurstSound && Managers.Instance != null && Managers.Instance.audioManager != null)
            {
                // 「ジャララッ！」という音（専用SEがあれば使用、なければ通常のCoin SE）
                AudioClip burstClip = Managers.Instance.audioManager.CoinBurst != null
                    ? Managers.Instance.audioManager.CoinBurst
                    : Managers.Instance.audioManager.Coin;

                if (burstClip != null)
                {
                    Managers.Instance.audioManager.PlaySound(burstClip);
                }
            }
        }

        /// <summary>
        /// コインを時間差で生成するコルーチン
        /// </summary>
        private IEnumerator SpawnCoinsWithDelay(Vector3 position, int coinCount, long goldPerCoin)
        {
            for (int i = 0; i < coinCount; i++)
            {
                SpawnSingleCoin(position, goldPerCoin);

                // 少し待つ（時間差で飛び散る演出）
                if (coinSpawnInterval > 0)
                {
                    yield return new WaitForSeconds(coinSpawnInterval);
                }
            }
        }

        /// <summary>
        /// 1枚のコインを生成して飛ばす
        /// </summary>
        private void SpawnSingleCoin(Vector3 position, long goldValue)
        {
            // コインを生成
            GameObject coinObj = Instantiate(coinPrefab, position, Quaternion.identity);
            Coin coin = coinObj.GetComponent<Coin>();

            if (coin == null)
            {
                Debug.LogError("CoinSpawner: coinPrefab に Coin コンポーネントがありません！");
                Destroy(coinObj);
                return;
            }

            // ランダムな方向に力を加える（上方向にバイアス）
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector2 force = new Vector2(
                randomDirection.x * Random.Range(minForce, maxForce),
                Mathf.Abs(randomDirection.y) * Random.Range(minForce, maxForce) * upwardBias
            );

            // コインの価値を設定
            coin.SetGoldValue(goldValue);

            // コインを飛ばす
            coin.Burst(force, coinUITarget);
        }

        /// <summary>
        /// Inspectorでのデバッグ用：爆発範囲を可視化
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
