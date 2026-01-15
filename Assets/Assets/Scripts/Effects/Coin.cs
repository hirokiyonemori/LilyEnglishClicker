using UnityEngine;
using System.Collections;

namespace Idle
{
    /// <summary>
    /// 敵を倒した時に飛び散るコインの制御
    /// 物理的に飛び散った後、UIゴールド表示に吸い込まれる演出
    /// </summary>
    public class Coin : MonoBehaviour
    {
        [Header("設定")]
        [SerializeField] private float burstDuration = 0.7f; // 飛び散ってから吸い込まれるまでの時間
        [SerializeField] private float flySpeed = 8f; // UI に吸い込まれる速度
        [SerializeField] private float collectDistance = 0.5f; // UIに到達したと判定する距離

        private long goldValue = 1; // このコイン1枚の価値（外部から設定可能）

        private Rigidbody2D rb;
        private Transform targetUI;
        private bool isFlying = false;
        private bool isCollected = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogWarning("Coin: Rigidbody2D が見つかりません。動的に追加します。");
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
        }

        /// <summary>
        /// このコインの価値を設定
        /// </summary>
        /// <param name="value">ゴールド価値</param>
        public void SetGoldValue(long value)
        {
            goldValue = value;
        }

        /// <summary>
        /// コインを飛び散らせる
        /// </summary>
        /// <param name="force">加える力（Vector2）</param>
        /// <param name="uiTarget">吸い込み先のUI Transform（ゴールド表示）</param>
        public void Burst(Vector2 force, Transform uiTarget)
        {
            targetUI = uiTarget;

            // 物理演算で飛び散る
            rb.AddForce(force, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-20f, 20f)); // 回転で躍動感を出す

            // 一定時間後にUIへ飛び始める
            StartCoroutine(StartFlyRoutine());
        }

        /// <summary>
        /// 飛び散り完了後、UI吸い込みモードに切り替え
        /// </summary>
        private IEnumerator StartFlyRoutine()
        {
            yield return new WaitForSeconds(burstDuration);

            // 物理演算をオフにして、スクリプト制御に切り替え
            if (rb != null)
            {
                rb.simulated = false;
            }
            isFlying = true;
        }

        private void Update()
        {
            if (isFlying && !isCollected && targetUI != null)
            {
                // UIに向かって滑らかに移動
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetUI.position,
                    Time.deltaTime * flySpeed
                );

                // UIに十分近づいたら回収
                if (Vector3.Distance(transform.position, targetUI.position) < collectDistance)
                {
                    CollectCoin();
                }
            }
        }

        /// <summary>
        /// コインをUIに回収（ゴールド加算 & 演出）
        /// </summary>
        private void CollectCoin()
        {
            if (isCollected) return; // 二重回収防止
            isCollected = true;

            // ゴールドを加算
            if (Managers.Instance != null && Managers.Instance.gameManager != null)
            {
                Managers.Instance.gameManager.AddReward(goldValue, 0);
            }

            // 「チャリン！」SE 再生（専用SEがあれば使用、なければ通常のCoin SE）
            if (Managers.Instance != null && Managers.Instance.audioManager != null)
            {
                AudioClip collectClip = Managers.Instance.audioManager.CoinCollect != null
                    ? Managers.Instance.audioManager.CoinCollect
                    : Managers.Instance.audioManager.Coin;

                if (collectClip != null)
                {
                    Managers.Instance.audioManager.PlaySound(collectClip);
                }
            }

            // UI のゴールドテキストをバウンドさせる（既存の UpdateGoldAndScore で対応済み）

            // コインを破棄
            Destroy(gameObject);
        }

        /// <summary>
        /// ターゲットが無効な場合の自動破棄（安全機構）
        /// </summary>
        private void OnBecameInvisible()
        {
            // 画面外に出て一定時間経っても戻らなければ破棄
            if (!isFlying)
            {
                Invoke(nameof(SafeDestroy), 3f);
            }
        }

        private void SafeDestroy()
        {
            if (this != null && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
