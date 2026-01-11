using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 魔法弾（プロジェクタイル）
    /// タップした瞬間にリリィの杖から飛び出し、敵を倒す
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("=== 弾の性能 ===")]
        [Tooltip("弾の速さ（速いほうが爽快）")]
        public float speed = 15f;
        
        [Tooltip("敵に与えるダメージ")]
        public int damage = 1;
        
        [Tooltip("画面外に出たら消える時間（秒）")]
        public float lifeTime = 2.0f;

        [Header("=== 当たった時の演出 ===")]
        [Tooltip("爆発やキラキラのエフェクト")]
        public GameObject hitEffectPrefab;

        private Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            
            // 重力を無効化（重力で落ちないように）
            rb.gravityScale = 0f;
            
            // コライダーをトリガーに設定
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        void Start()
        {
            // 生成された瞬間に「右」へ向かって加速
            rb.linearVelocity = transform.right * speed;

            // ずっと残り続けると重くなるので、一定時間で消す
            Destroy(gameObject, lifeTime);
        }

        /// <summary>
        /// 何かにぶつかった時の処理
        /// </summary>
        void OnTriggerEnter2D(Collider2D hitInfo)
        {
            // ぶつかった相手が「敵 (EnemyController)」を持っているか確認
            EnemyController enemy = hitInfo.GetComponent<EnemyController>();
            
            if (enemy != null)
            {
                // 敵にダメージを与える
                enemy.TakeDamage(damage);

                // ヒット演出（爆発など）があれば生成する
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                }

                // この弾自体は消滅する（貫通させない場合）
                Destroy(gameObject);
            }
        }
    }
}
