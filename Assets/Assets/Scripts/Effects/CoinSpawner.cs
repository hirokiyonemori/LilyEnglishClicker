using UnityEngine;
using System.Collections;

namespace Idle
{
    /// <summary>
    /// コインエクスプロージョンを管理するクラス
    /// 敵を倒した時に、複数のコインを四方八方に飛び散らせる
    ///
    /// Coin は RectTransform+Image のUI要素のため、必ずCanvas配下に
    /// 生成する。以前はワールド空間に親無しでInstantiateしており、
    /// Canvas祖先を持たないImageは描画されない状態だった。
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        [Header("コイン設定")]
        [SerializeField] private GameObject coinPrefab; // コインのPrefab
        [SerializeField] private Transform coinUITarget; // コインが飛んでいく先（UIのゴールド表示）
        [SerializeField] private Transform coinParent; // コインの生成先。未設定ならcoinUITargetの属するCanvasを自動使用

        [Header("エクスプロージョン設定")]
        [SerializeField] private int minCoinCount = 10; // 最小コイン数
        [SerializeField] private int maxCoinCount = 20; // 最大コイン数
        [SerializeField] private float explosionRadius = 2f; // 爆発半径（Gizmo表示用）
        [Tooltip("飛び散る距離（Canvasのピクセル単位）。UI移動に変更したためRigidbody2D時代の値からスケールを変えている")]
        [SerializeField] private float minBurstDistance = 80f;
        [SerializeField] private float maxBurstDistance = 220f;
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
                // 同じGameObjectに他のマネージャーが同居している構成もあるため、
                // GameObject自体ではなくこのコンポーネントだけを破棄する
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            ResolveTargets();
        }

        /// <summary>
        /// UIのゴールド表示と、コインを生成するCanvasを解決する
        /// </summary>
        private void ResolveTargets()
        {
            if (coinUITarget == null)
            {
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

            if (coinParent == null && coinUITarget != null)
            {
                Canvas canvas = coinUITarget.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    coinParent = canvas.transform;
                }
                else
                {
                    Debug.LogWarning("CoinSpawner: coinUITarget の親にCanvasが見つかりません。coinParentを手動設定してください。");
                }
            }
        }

        /// <summary>
        /// コインエクスプロージョンを発生させる
        /// </summary>
        /// <param name="position">爆発位置（ワールド座標。UI CanvasのRectTransform位置でよい）</param>
        /// <param name="totalGold">総ゴールド報酬（コインの価値の合計）</param>
        public void SpawnCoinExplosion(Vector3 position, long totalGold)
        {
            if (totalGold <= 0)
            {
                return;
            }

            if (coinPrefab == null)
            {
                Debug.LogError("CoinSpawner: coinPrefab が設定されていません！");
                return;
            }

            if (coinUITarget == null || coinParent == null)
            {
                ResolveTargets();

                if (coinUITarget == null || coinParent == null)
                {
                    Debug.LogError("CoinSpawner: UIターゲットまたは生成先Canvasが見つかりません。コイン演出をスキップします。");
                    return;
                }
            }

            // コイン数を決定（総額より多くの枚数にはしない＝価値0のコインを防ぐ）
            int coinCount = Random.Range(minCoinCount, maxCoinCount + 1);
            coinCount = (int)Mathf.Min(coinCount, totalGold);
            coinCount = Mathf.Max(coinCount, 1);

            long[] values = DistributeGold(totalGold, coinCount);

            // コルーチンで時間差生成
            StartCoroutine(SpawnCoinsWithDelay(position, values));

            // コイン飛び散りSEを再生
            if (playBurstSound && Managers.Instance != null && Managers.Instance.audioManager != null)
            {
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
        /// totalGold を coinCount 枚へ過不足なく配分する。
        /// 単純な整数除算（totalGold / coinCount）だと端数が切り捨てられ、
        /// 合計が totalGold に一致しない（最大で数十%欠損する）問題があったため、
        /// 余りを先頭のコインから1ずつ配って必ず合計が一致するようにしている。
        /// </summary>
        private static long[] DistributeGold(long totalGold, int coinCount)
        {
            long[] values = new long[coinCount];
            if (coinCount <= 0) return values;

            long baseValue = totalGold / coinCount;
            long remainder = totalGold - baseValue * coinCount;

            for (int i = 0; i < coinCount; i++)
            {
                values[i] = baseValue + (i < remainder ? 1 : 0);
            }

            return values;
        }

        /// <summary>
        /// コインを時間差で生成するコルーチン
        /// </summary>
        private IEnumerator SpawnCoinsWithDelay(Vector3 position, long[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                SpawnSingleCoin(position, values[i]);

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
            // 必ずCanvas配下に生成する（Coinのm_RaycastTarget等はprefab側でOFF済み）
            GameObject coinObj = Instantiate(coinPrefab, coinParent);
            coinObj.transform.position = position; // 同一Canvas平面上のワールド座標をそのまま使う

            Coin coin = coinObj.GetComponent<Coin>();
            if (coin == null)
            {
                Debug.LogError("CoinSpawner: coinPrefab に Coin コンポーネントがありません！");
                Destroy(coinObj);
                return;
            }

            // ランダムな方向・距離を「飛び散る先のオフセット」として渡す（上方向にバイアス）
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minBurstDistance, maxBurstDistance);
            Vector2 burstOffset = new Vector2(
                randomDirection.x * distance,
                Mathf.Abs(randomDirection.y) * distance * upwardBias
            );

            coin.SetGoldValue(goldValue);
            coin.Burst(burstOffset, coinUITarget);
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
