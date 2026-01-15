using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 標準Textを使うために必須

namespace Idle
{
    /// <summary>
    /// 敵キャラクターを制御するコンポーネント
    /// 英単語を表示し、タップによるダメージとHP管理を行います
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [Header("UI参照")]
        [SerializeField] private Text englishText;
        [SerializeField] private Text readingText;
        [SerializeField] private Image enemyImage; // ダメージ演出用

        [Header("ダメージ演出設定")]
        [SerializeField] private Color damageColor = Color.red;
        [SerializeField] private float damageEffectDuration = 0.1f;
        [SerializeField] private float shakeAmount = 0.01f;
        [SerializeField] private float shakeDuration = 0.1f;

        // 内部データ
        private WordData currentWordData;
        private int currentHp;
        private int maxHp;
        private Color originalColor;
        private Vector3 originalPosition;

        private void Awake()
        {
            // 元の色と位置を保存
            if (enemyImage != null)
            {
                originalColor = enemyImage.color;
            }
            originalPosition = transform.localPosition;
        }

        /// <summary>
        /// 敵を初期化（生成時にGameManagerから呼ばれる）
        /// </summary>
        /// <param name="data">表示する単語データ</param>
        public void Setup(WordData data)
        {
            if (data == null)
            {
                Debug.LogError("EnemyController.Setup: WordDataがnullです。");
                return;
            }

            currentWordData = data;

            // テキスト反映
            if (englishText != null)
            {
                englishText.text = data.english;
            }
            else
            {
                Debug.LogWarning("EnemyController: englishTextが設定されていません。");
            }

            if (readingText != null)
            {
                readingText.text = data.reading;
            }
            else
            {
                Debug.LogWarning("EnemyController: readingTextが設定されていません。");
            }

            // HP設定
            maxHp = 1;
            currentHp = maxHp;

            Debug.Log($"敵生成: {data.english} (HP: {currentHp})");
        }

        /// <summary>
        /// タップされた時の処理（ButtonのOnClick、または外部から呼ぶ）
        /// </summary>
        public void OnTap()
        {
            // とりあえず1ダメージ。後で DataManager.data.MoneyByClick などから攻撃力を参照する
            //int damage = 1;

            // 既存のゲームシステムと統合する場合はこのようにする:
            int damage = (int)DataManager.data.MoneyByClick;

            TakeDamage(damage);

            // サウンド再生（既存のAudioManagerを使用）
            if (Managers.Instance != null && Managers.Instance.audioManager != null)
            {
                Managers.Instance.audioManager.PlaySound(Managers.Instance.audioManager.Coin);
            }
        }

        /// <summary>
        /// ダメージ処理
        /// </summary>
        /// <param name="damage">与えるダメージ量</param>
        public void TakeDamage(int damage)
        {
            currentHp -= damage;

            Debug.Log($"{currentWordData.english} にダメージ: {damage} (残りHP: {currentHp}/{maxHp})");

            // 演出：ダメージを受けたら少し赤くする、震える
            StartCoroutine(DamageEffect());

            // カメラシェイク（既存のシステムを使用）
            if (Camera.main != null)
            {
                StartCoroutine(CameraShake.Shake(Camera.main.transform, shakeDuration, shakeAmount));
            }

            // HP確認
            if (currentHp <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 敵が倒された時の処理
        /// </summary>
        private void Die()
        {
            Debug.Log($"{currentWordData.english} を倒しました！");

            // パーティクル再生（既存のシステムを使用）
            if (Managers.Instance != null && Managers.Instance.particleManager != null)
            {
                Managers.Instance.particleManager.PlayEffect(Managers.Instance.particleManager.clickEffect);
            }

            // ★コインエクスプロージョン（報酬処理の前に実行）
            SpawnCoinExplosion();

            // 報酬処理
            GiveReward();

            // GameManagerに「倒したよ」と通知
            if (Managers.Instance != null && Managers.Instance.gameManager != null)
            {
                Managers.Instance.gameManager.OnEnemyDefeated(this);
            }

            // コルーチンで破棄（演出完了を待つ）
            StartCoroutine(DestroyAfterDelay(damageEffectDuration + 0.05f));
        }

        /// <summary>
        /// 遅延後にオブジェクトを破棄
        /// </summary>
        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }

        /// <summary>
        /// コインエクスプロージョンを生成
        /// </summary>
        private void SpawnCoinExplosion()
        {
            // 報酬額を計算（GiveRewardと同じロジック）
            int wordLength = currentWordData.english.Length;
            long goldReward = wordLength * 10; // 1文字あたり10ゴールド

            // CoinSpawner を使ってコインを飛び散らせる
            if (CoinSpawner.Instance != null)
            {
                CoinSpawner.Instance.SpawnCoinExplosion(transform.position, goldReward);
            }
            else
            {
                Debug.LogWarning("EnemyController: CoinSpawner が見つかりません。コインエクスプロージョンをスキップします。");
            }
        }

        /// <summary>
        /// 敵を倒した時の報酬処理
        /// </summary>
        private void GiveReward()
        {
            // ★新しい報酬システム：文字数に応じた報酬
            // 長い単語ほど報酬が高い（例: Apple(5文字) = 50 Gold）
            int wordLength = currentWordData.english.Length;
            long goldReward = wordLength * 10; // 1文字あたり10ゴールド
            int scoreReward = 1; // 1体倒すごとに+1スコア

            // ★コイン演出に報酬を任せるため、ここでは GameManager のスコアのみ更新
            // コインが UI に到達した時に Coin.cs が AddReward を呼び出す

            // 既存のMoneyシステムにも追加（互換性のため）
            // DataManager.data.Money += goldReward; // ← コインが加算するので重複回避

            Debug.Log($"報酬獲得: {goldReward} Gold (単語の長さ: {wordLength}文字)");

            // ★GameManagerの新しい報酬システムに報告（スコアのみ）
            if (Managers.Instance != null && Managers.Instance.gameManager != null)
            {
                Managers.Instance.gameManager.AddReward(0, scoreReward); // ゴールドは0、スコアのみ加算
            }

            // UI更新（既存のシステムも引き続き更新）
            if (Managers.Instance != null && Managers.Instance.uIManager != null)
            {
                Managers.Instance.uIManager.UpdateUI();
            }

            // データ保存
            DataManager.SaveData();
        }

        /// <summary>
        /// 簡易ダメージ演出（一瞬赤くなる）
        /// </summary>
        private IEnumerator DamageEffect()
        {
            if (enemyImage != null)
            {
                Debug.Log($"★DamageEffect開始: 色を {damageColor} に変更"); // デバッグログ追加
                enemyImage.color = damageColor;
                yield return new WaitForSeconds(damageEffectDuration);
                enemyImage.color = originalColor;
                Debug.Log($"★DamageEffect完了: 色を {originalColor} に戻した"); // デバッグログ追加
            }
            else
            {
                Debug.LogWarning("★DamageEffect: enemyImage が null のため実行されませんでした！"); // 警告追加
            }
        }

        /// <summary>
        /// 現在のHP割合を取得（0.0 ~ 1.0）
        /// </summary>
        public float GetHpRatio()
        {
            if (maxHp <= 0) return 0f;
            return (float)currentHp / maxHp;
        }

        /// <summary>
        /// 現在のHPを取得
        /// </summary>
        public int GetCurrentHp()
        {
            return currentHp;
        }

        /// <summary>
        /// 最大HPを取得
        /// </summary>
        public int GetMaxHp()
        {
            return maxHp;
        }

        /// <summary>
        /// 現在の単語データを取得
        /// </summary>
        public WordData GetWordData()
        {
            return currentWordData;
        }

        private void OnDestroy()
        {
            // すべてのコルーチンを停止
            StopAllCoroutines();
        }
    }
}
