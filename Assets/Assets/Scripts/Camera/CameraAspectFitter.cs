using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 正射影カメラの縦横比自動調整。
    ///
    /// 正射影カメラは縦の表示範囲が orthographicSize で固定される一方、
    /// 横の表示範囲は画面のアスペクト比に依存する。このゲームの背景・UIは
    /// 1080x1920（縦長）を想定して作られているため、それより縦長な画面
    /// （ノッチ付き端末や一部シミュレータなど）で再生すると、横方向の
    /// 表示範囲が想定より狭くなり、画面端のUIが切れて見える。
    ///
    /// これを防ぐため、想定より縦長な画面ではorthographicSizeを大きくして
    /// 常に想定した横幅がすべて見えるようにする（代わりに上下がやや
    /// 多く見えることはあるが、UIが切れるより実害が小さい）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraAspectFitter : MonoBehaviour
    {
        [Tooltip("背景・UIが設計された基準の横幅（Canvas Scalerの参照解像度と合わせる）")]
        [SerializeField] private float designWidth = 1080f;

        [Tooltip("背景・UIが設計された基準の縦幅（Canvas Scalerの参照解像度と合わせる）")]
        [SerializeField] private float designHeight = 1920f;

        [Tooltip("designWidth/designHeightで想定した縦の表示範囲（orthographicSize）")]
        [SerializeField] private float baseOrthographicSize = 3.04f;

        private Camera targetCamera;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            // エディタでシミュレータの端末を切り替えた場合などに追従する
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            float designAspect = designWidth / designHeight;
            float screenAspect = (float)Screen.width / Screen.height;

            // 画面が設計時より縦長（横幅の比率が小さい）場合のみズームアウトして
            // 横方向の見切れを防ぐ。設計時と同じか横長な画面では基準サイズのまま
            targetCamera.orthographicSize = screenAspect < designAspect
                ? baseOrthographicSize * (designAspect / screenAspect)
                : baseOrthographicSize;
        }
    }
}
