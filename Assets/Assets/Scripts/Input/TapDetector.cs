using UnityEngine;
using UnityEngine.EventSystems;

namespace Idle
{
    /// <summary>
    /// 画面全体のタップを検出してGameManager.Click()を呼び出す
    /// </summary>
    public class TapDetector : MonoBehaviour
    {
        private GameManager gameManager;

        private void Start()
        {
            // GameManagerへの参照を取得
            gameManager = FindObjectOfType<GameManager>();

            if (gameManager == null)
            {
                Debug.LogError("TapDetector: GameManagerが見つかりません！");
            }
        }

        private void Update()
        {
            // マウスクリック or タッチ入力を検出
            if (Input.GetMouseButtonDown(0))
            {
                // UI要素をタップした場合は無視
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    Debug.Log("TapDetector: UI上のタップなので無視");
                    return;
                }

                Debug.Log("TapDetector: タップ検出！GameManager.Click()を呼び出します");

                if (gameManager != null)
                {
                    gameManager.Click();
                }
            }
        }
    }
}
