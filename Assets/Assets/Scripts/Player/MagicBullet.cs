using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 魔法弾の動きを制御するスクリプト
    /// </summary>
    public class MagicBullet : MonoBehaviour
    {
        [Header("設定")]
        [SerializeField] private float speed = 1000f; // 弾の速さ（UIの場合は大きめにする）
        [SerializeField] private float lifeTime = 1.0f; // 消えるまでの時間（秒）

        private void Start()
        {
            // 一定時間後に自動で消滅
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            // 右方向に移動
            // UIの場合は rectTransform.anchoredPosition を動かす方が良いが、
            // Translateでもワールド座標系で動くので基本的にはOK
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
    }
}
