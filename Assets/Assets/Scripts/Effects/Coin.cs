using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// 敵を倒した時に飛び散るコインの制御。
    /// 敵・ゴールド表示ともにUI(Image/Text)なので、コインもUI空間で完結させる。
    /// 以前はRigidbody2D + AddForceで物理的に飛ばしていたが、
    /// Canvas配下に生成されないと描画されない・座標系が噛み合わないという
    /// 問題があったため、Transform.position（Canvas平面上のワールド座標）を
    /// 直接補間する方式に変更した。
    /// </summary>
    public class Coin : MonoBehaviour
    {
        [Header("設定")]
        [SerializeField] private float burstDuration = 0.25f; // 飛び散る演出の時間
        [SerializeField] private float flyDuration = 0.5f;    // UIに吸い込まれるまでの時間
        [Tooltip("演出が何らかの理由で止まった場合の保険。この秒数を過ぎたら報酬を回収して終了する")]
        [SerializeField] private float hardTimeout = 5f;

        private long goldValue = 1; // このコイン1枚の価値（外部から設定可能）
        private bool isCollected = false;
        private bool launched;
        private Image image;
        private Color tint;
        private Vector3 fullScale;

        private void Awake()
        {
            image = GetComponent<Image>();
            fullScale = transform.localScale;
            if (image != null) { tint = image.color; image.color = new Color(tint.r, tint.g, tint.b, 0); }
        }

        private void SetAppearance(float scale, float alpha)
        {
            transform.localScale = fullScale * scale;
            if (image != null) image.color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
        }

        /// <summary>
        /// このコインの価値を設定
        /// </summary>
        public void SetGoldValue(long value)
        {
            goldValue = value;
        }

        /// <summary>
        /// コインを飛び散らせる
        /// </summary>
        /// <param name="burstOffset">飛び散る先へのオフセット（Canvasのピクセル単位）</param>
        /// <param name="uiTarget">吸い込み先のUI Transform（ゴールド表示）</param>
        public void Burst(Vector2 burstOffset, Transform uiTarget)
        {
            if (launched) return;
            launched = true;
            // どんな経路でも一定時間後には必ず消える保険。
            // UI(CanvasRenderer)にはOnBecameInvisibleが発火しないため、
            // それに頼っていた旧実装ではコインが永久に残るケースがあった。
            Invoke(nameof(CollectCoin), Mathf.Max(.1f, hardTimeout));

            StartCoroutine(FlightRoutine(burstOffset, uiTarget));
        }

        private IEnumerator FlightRoutine(Vector2 burstOffset, Transform uiTarget)
        {
            // Distances are Canvas units, not screen/world pixels (CanvasScaler can differ).
            Vector3 startPos = transform.localPosition;
            Vector3 burstTargetPos = startPos + (Vector3)burstOffset;

            // ─ フェーズ1: 弾け飛ぶ（イーズアウト） ─
            float t = 0f;
            while (t < burstDuration)
            {
                t += Time.deltaTime;
                float p = EaseOutQuad(Mathf.Clamp01(t / burstDuration));
                transform.localPosition = Vector3.Lerp(startPos, burstTargetPos, p);
                SetAppearance(Mathf.Lerp(.65f, 1f, p), Mathf.Clamp01(t / .06f));
                yield return null;
            }
            transform.localPosition = burstTargetPos;

            // ─ フェーズ2: UIへ吸い込まれる ─
            // uiTargetが無ければその場で回収扱いにする
            if (uiTarget == null)
            {
                CollectCoin();
                yield break;
            }

            Vector3 flyStart = transform.localPosition;
            t = 0f;
            while (t < flyDuration)
            {
                t += Time.deltaTime;
                float p = EaseInQuad(Mathf.Clamp01(t / flyDuration));
                // ターゲット（ゴールド表示）自体も動きうるので毎フレーム位置を取り直す
                if (uiTarget == null) { CollectCoin(); yield break; }
                Vector3 target = transform.parent != null ? transform.parent.InverseTransformPoint(uiTarget.position) : uiTarget.position;
                transform.localPosition = Vector3.Lerp(flyStart, target, p);
                SetAppearance(Mathf.Lerp(1f, .45f, p), 1f - Mathf.InverseLerp(.8f, 1f, p));
                yield return null;
            }

            CollectCoin();
        }

        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
        private static float EaseInQuad(float x) => x * x;

        /// <summary>
        /// コインをUIに回収（ゴールド加算 & 演出）
        /// </summary>
        private void CollectCoin()
        {
            if (isCollected) return; // 二重回収防止
            isCollected = true;
            CancelInvoke(nameof(CollectCoin));

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

            // コインを破棄
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
