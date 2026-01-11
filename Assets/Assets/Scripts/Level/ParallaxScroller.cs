using UnityEngine;

namespace Idle
{
    /// <summary>
    /// パララックス（視差）スクロール
    /// 複数の背景レイヤーを異なる速度で動かして奥行きを表現
    /// </summary>
    public class ParallaxScroller : MonoBehaviour
    {
        [Header("レイヤー設定")]
        [Tooltip("背景レイヤー（奥から順に）")]
        [SerializeField] private ParallaxLayer[] layers;

        [Header("制御")]
        [SerializeField] private bool isScrolling = false;
        [SerializeField] private float baseSpeed = 2f; // 基準速度

        /// <summary>
        /// 背景レイヤーの設定
        /// </summary>
        [System.Serializable]
        public class ParallaxLayer
        {
            [Tooltip("このレイヤーのTransform")]
            public Transform layerTransform;

            [Tooltip("速度係数（1.0 = 基準速度、0.5 = 半分、2.0 = 2倍）")]
            [Range(0f, 2f)]
            public float speedMultiplier = 1f;

            [Tooltip("ループする幅（このレイヤーの横幅）")]
            public float loopWidth = 20f;

            // 内部状態
            [HideInInspector]
            public Vector3 startPosition;
        }

        private void Start()
        {
            // 各レイヤーの初期位置を記録
            foreach (var layer in layers)
            {
                if (layer.layerTransform != null)
                {
                    layer.startPosition = layer.layerTransform.position;
                }
            }
        }

        private void Update()
        {
            if (!isScrolling) return;

            foreach (var layer in layers)
            {
                if (layer.layerTransform == null) continue;

                // レイヤーごとの速度で左に移動
                float speed = baseSpeed * layer.speedMultiplier;
                layer.layerTransform.Translate(Vector3.left * speed * Time.deltaTime);

                // ループ処理：一定距離左に行ったら右端に戻る
                if (layer.layerTransform.position.x <= layer.startPosition.x - layer.loopWidth)
                {
                    Vector3 newPos = layer.layerTransform.position;
                    newPos.x += layer.loopWidth * 2; // 2枚分の幅だけ右に戻す
                    layer.layerTransform.position = newPos;
                }
            }
        }

        /// <summary>
        /// スクロールの ON/OFF
        /// </summary>
        public void SetScrolling(bool scrolling)
        {
            Debug.Log($"★ParallaxScroller: SetScrolling({scrolling})");
            isScrolling = scrolling;
        }

        /// <summary>
        /// 基準速度を変更
        /// </summary>
        public void SetBaseSpeed(float speed)
        {
            baseSpeed = speed;
        }
    }
}
