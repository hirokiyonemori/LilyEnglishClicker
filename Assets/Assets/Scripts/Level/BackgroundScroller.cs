using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    public class BackgroundScroller : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float scrollSpeed = 0.5f;
        [SerializeField] private bool isScrolling = false;

        private Material targetMaterial;
        private Vector2 currentOffset;

        private void Start()
        {
            // Try to get material from Renderer (3D/Quad) or Image (UI)
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                targetMaterial = rend.material;
            }
            else
            {
                Image img = GetComponent<Image>();
                if (img != null)
                {
                    targetMaterial = img.material;
                }
            }

            if (targetMaterial == null)
            {
                Debug.LogWarning("BackgroundScroller: No material found on this object.");
            }
            else
            {
                // 初期オフセットを取得
                currentOffset = targetMaterial.mainTextureOffset;
            }
        }

        private void Update()
        {
            if (isScrolling && targetMaterial != null)
            {
                // 前フレームからの差分を加算していく方式
                currentOffset.x += Time.deltaTime * scrollSpeed;

                // オフセットが1を超えたら0に戻す（テクスチャは繰り返しなので見た目は同じ）
                if (currentOffset.x >= 1f)
                {
                    currentOffset.x -= 1f;
                }

                targetMaterial.mainTextureOffset = currentOffset;
            }
        }

        public void SetScrolling(bool scrolling)
        {
            Debug.Log($"★BackgroundScroller: SetScrolling({scrolling}) 呼び出し");
            isScrolling = scrolling;
        }
    }
}
