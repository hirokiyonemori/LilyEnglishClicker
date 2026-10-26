using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Idle
{
    /// <summary>
    /// 敵キャラクターを制御するコンポーネント
    /// 敵名を表示し、タップによるダメージとHP管理を行います
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [Header("UI参照（敵の上の Enemy HUD）")]
        [Tooltip("敵名の表示先。保存済みPrefab参照との互換性のためフィールド名を維持")]
        [SerializeField] private TMP_Text englishText;
        [SerializeField] private TMP_Text readingText;
        [SerializeField] private TextMeshProUGUI hpText; // HP の数値（未設定でも動作する）
        [Tooltip("HP ゲージの中身。anchorMax.x を残り HP の割合にする")]
        [SerializeField] private RectTransform hpFill;

        [Header("見た目（接地・影）")]
        [SerializeField] private Image enemyImage; // ダメージ演出用
        [Tooltip("スプライトの「描かれている範囲」だけを表す箱。下端が地面（このオブジェクトの原点）に来る")]
        [SerializeField] private RectTransform body;
        [SerializeField] private RectTransform shadow;
        [SerializeField] private EnemySpriteBounds spriteBounds;
        [Tooltip("通常の敵の見た目の高さ。EnemyData.spriteScale を掛ける（ボスは大きく）")]
        [SerializeField] private float baseVisibleHeight = 300f;
        [Tooltip("見た目の横幅の上限（spriteScale を掛けた後、さらに maxVisibleWidthCap で頭打ち）")]
        [SerializeField] private float maxVisibleWidth = 340f;
        [Tooltip("画面右側に収めるための横幅の絶対上限")]
        [SerializeField] private float maxVisibleWidthCap = 470f;
        [SerializeField, Range(0.3f, 1.2f)] private float shadowWidthRatio = 0.85f;

        [Header("ダメージ演出設定")]
        [SerializeField] private Color damageColor = Color.red;
        [SerializeField] private float damageEffectDuration = 0.1f;
        [SerializeField] private float shakeAmount = 0.01f;
        [SerializeField] private float shakeDuration = 0.1f;

        // 内部データ
        private WordData currentWordData;
        private EnemyData currentEnemyData;
        private const int BossGoldFactor = 5; // ボスの撃破ゴールドは通常の何倍か
        private int currentHp;
        private int maxHp;
        private bool defeated;
        public RectTransform AttackVisual => body != null ? body : (enemyImage != null ? enemyImage.rectTransform : null);
        public int AttackDamage => currentEnemyData != null && currentEnemyData.attackDamage > 0
            ? currentEnemyData.attackDamage : Mathf.Max(1, (currentEnemyData != null ? currentEnemyData.tier : 1) * 2);
        public float AttackInterval(float fallback) => currentEnemyData != null && currentEnemyData.attackInterval > 0
            ? Mathf.Max(.1f, currentEnemyData.attackInterval) : fallback;
        private Color originalColor;
        private Vector2 baseImageSize;
        private float autoDamageCarry;
        private float damageCarry; // タップ威力の端数（次のタップへ持ち越す）

        private void Awake()
        {
            // 元の色を保存
            if (enemyImage != null)
            {
                originalColor = enemyImage.color;
                baseImageSize = enemyImage.rectTransform.sizeDelta;
            }
        }

        /// <summary>
        /// 敵を初期化（生成時にGameManagerから呼ばれる）
        /// </summary>
        /// <param name="data">旧単語データ（本番はnull。既存の呼び出し互換用）</param>
        /// <param name="enemyData">見た目と強さの設定。nullの場合はプレハブ既定の見た目・HP1で動作する</param>
        public void Setup(WordData data, EnemyData enemyData = null)
        {
            currentWordData = data;
            currentEnemyData = enemyData;

            // 見た目の反映
            ApplyAppearance(enemyData);

            // 本番では単語ではなく敵キャラクターの名前を表示する。
            if (englishText != null) englishText.text = enemyData != null ? enemyData.displayName : "まもの";
            if (readingText != null) readingText.gameObject.SetActive(false);
            // ステージが進むほど硬くなる（ステージ1は等倍）
            maxHp = enemyData != null ? Mathf.Max(1, Mathf.RoundToInt(enemyData.baseHp * StageProgress.EnemyHpMultiplier)) : 1;
            defeated = false;
            currentHp = maxHp;
            UpdateHpText();

            string enemyName = enemyData != null ? enemyData.displayName : "(既定)";
            Debug.Log($"敵生成: {enemyName} (HP: {currentHp})");
        }

        /// <summary>
        /// HP表示テキストを現在値に合わせて更新する
        /// </summary>
        private void UpdateHpText()
        {
            int shown = Mathf.Max(0, currentHp);
            if (hpText != null) hpText.text = $"{shown:N0}/{maxHp:N0}";
            if (hpFill != null)
            {
                float ratio = maxHp > 0 ? Mathf.Clamp01((float)shown / maxHp) : 0f;
                hpFill.anchorMax = new Vector2(ratio, hpFill.anchorMax.y);
            }
        }

        /// <summary>
        /// EnemyDataの見た目をこのインスタンスに反映する
        /// </summary>
        private void ApplyAppearance(EnemyData enemyData)
        {
            if (enemyImage == null)
            {
                return;
            }

            if (enemyData != null)
            {
                if (enemyData.sprite != null)
                {
                    enemyImage.sprite = enemyData.sprite;
                }
                else
                {
                    Debug.LogWarning($"EnemyController: {enemyData.displayName} にスプライトが設定されていません。");
                }
            }

            float scale = enemyData != null && enemyData.spriteScale > 0f ? enemyData.spriteScale : 1f;

            if (body == null)
            {
                // 旧構成のプレハブ（接地用の body が無い）では従来どおり画像サイズだけ拡縮する
                enemyImage.rectTransform.sizeDelta = baseImageSize * scale;
                return;
            }

            LayoutGrounded(scale);
        }

        /// <summary>
        /// スプライトの「描かれている範囲」を body にぴったり合わせ、その下端を地面（このオブジェクトの原点）に置く。
        /// 画像ごとに透明な余白の量が違うため、画像の枠ではなく描かれている範囲で大きさと接地位置を決める。
        /// </summary>
        private void LayoutGrounded(float scale)
        {
            Sprite sprite = enemyImage.sprite;
            Rect visible = new Rect(0f, 0f, 1f, 1f);
            if (sprite != null && spriteBounds != null) spriteBounds.TryGet(sprite, out visible);

            float spriteAspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            float visibleAspect = visible.height > 0f ? spriteAspect * visible.width / visible.height : 1f;

            float height = baseVisibleHeight * scale;
            float width = height * visibleAspect;
            float widthLimit = Mathf.Min(maxVisibleWidth * scale, maxVisibleWidthCap);
            if (width > widthLimit)
            {
                width = widthLimit;
                height = width / visibleAspect;
            }

            body.sizeDelta = new Vector2(width, height);

            // 画像全体を、描かれている範囲が body に一致するように広げる（アンカーは body 基準の比率）
            RectTransform img = enemyImage.rectTransform;
            img.anchorMin = new Vector2(-visible.xMin / visible.width, -visible.yMin / visible.height);
            img.anchorMax = new Vector2((1f - visible.xMin) / visible.width, (1f - visible.yMin) / visible.height);
            img.offsetMin = Vector2.zero;
            img.offsetMax = Vector2.zero;

            if (shadow != null)
            {
                float shadowWidth = width * shadowWidthRatio;
                shadow.sizeDelta = new Vector2(shadowWidth, Mathf.Max(18f, shadowWidth * 0.2f));
            }
        }

        /// <summary>
        /// タップされた時の処理（ButtonのOnClick、または外部から呼ぶ）
        /// </summary>
        public void OnTap() => OnTap(SfxDirector.Attack);

        public void OnTap(string attackSound) => OnPlayerAttack(attackSound, 1f, false);

        public void OnPlayerAttack(string attackSound, float power, bool automatic)
        {
            if (defeated) return;
            // Lily の攻撃力（強化システムの ATK を威力へ換算した値）。強化システムが無い時は従来値。
            // 威力の端数は次のタップへ持ち越す（ATK を少し上げただけでも平均ダメージが上がるように）
            var growth = LilyEnglish.Growth.GrowthService.Instance;
            float attack = growth != null ? growth.AttackPowerExact : DataManager.data.MoneyByClick;
            float carry = automatic ? autoDamageCarry : damageCarry;
            carry += Mathf.Max(1f, attack * Mathf.Clamp01(power));
            int damage = Mathf.Max(1, Mathf.FloorToInt(carry));
            carry = Mathf.Max(0f, carry - damage);
            if (automatic) autoDamageCarry = carry; else damageCarry = carry;

            SfxDirector.Play(attackSound);
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
            if (defeated || damage <= 0) return;
            currentHp = Mathf.Max(0, currentHp - damage);
            UpdateHpText();

            string english = currentEnemyData != null ? currentEnemyData.displayName : "まもの";
            Debug.Log($"{english} にダメージ: {damage} (残りHP: {currentHp}/{maxHp})");

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
            if (defeated) return;
            defeated = true;
            // currentWordData が null のまま TakeDamage が呼ばれるケース
            // （Setup未完了・データ異常）でも落ちないようにする
            string english = currentEnemyData != null ? currentEnemyData.displayName : "まもの";
            Debug.Log($"{(english ?? "(不明な単語)")} を倒しました！");

            long goldReward = CalculateGoldReward();

            // ★コインエクスプロージョン（報酬処理の前に実行）
            SpawnCoinExplosion(goldReward);

            // 報酬処理
            GiveReward(goldReward);

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
        /// 撃破報酬（ゴールド）を計算する。
        /// SpawnCoinExplosion / GiveReward の双方で同じ値を使うよう一本化した
        /// （以前は2箇所に同じ式がベタ書きされており、片方だけ直すとズレる状態だった）。
        /// </summary>
        private long CalculateGoldReward()
        {
            long baseGold = currentEnemyData != null ? Mathf.Max(0, currentEnemyData.goldReward) : 50;
            // ボス（ティア5）は倒しがいのある報酬にする
            if (currentEnemyData != null && currentEnemyData.tier >= 5) baseGold *= BossGoldFactor;
            return (long)Mathf.Round(baseGold * StageProgress.GoldMultiplier);
        }

        /// <summary>
        /// コインエクスプロージョンを生成
        /// </summary>
        private void SpawnCoinExplosion(long goldReward)
        {
            // CoinSpawner を使ってコインを飛び散らせる
            if (CoinSpawner.Instance != null)
            {
                // コインは足元（原点）ではなく敵の体の中心から飛び散らせる
                Vector3 origin = body != null ? body.TransformPoint(body.rect.center) : transform.position;
                CoinSpawner.Instance.SpawnCoinExplosion(origin, goldReward);
            }
            else
            {
                // CoinSpawnerがシーンに存在しない場合、演出無しで即時加算する。
                // これが無いと「コインが出ない環境では報酬も出ない」という
                // 見えない形で報酬が消える不具合になる
                Debug.LogWarning("EnemyController: CoinSpawner が見つかりません。演出なしで報酬を直接加算します。");
                GrantGoldDirectly(goldReward);
            }
        }

        /// <summary>
        /// コイン演出を経由せず、直接ゴールドを加算する（フォールバック用）
        /// </summary>
        private void GrantGoldDirectly(long goldReward)
        {
            if (goldReward <= 0 || DataManager.data == null) return;

            // 所持金への加算は GameManager.AddReward が行う（ここで足すと二重になる）
            if (Managers.Instance != null && Managers.Instance.gameManager != null)
            {
                Managers.Instance.gameManager.AddReward(goldReward, 0);
            }
        }

        /// <summary>
        /// 敵を倒した時の報酬処理（スコアのみ。ゴールドはコイン演出/フォールバックが担当）
        /// </summary>
        private void GiveReward(long goldReward)
        {
            int scoreReward = 1; // 1体倒すごとに+1スコア

            // ★コイン演出に報酬を任せるため、ここでは GameManager のスコアのみ更新
            // コインが UI に到達した時に Coin.cs が AddReward を呼び出す

            Debug.Log($"報酬獲得: {goldReward} Gold (単語: {(currentWordData != null ? currentWordData.english : "不明")})");

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

        /// <summary>
        /// 現在の敵データを取得（未設定の場合はnull）
        /// </summary>
        public EnemyData GetEnemyData()
        {
            return currentEnemyData;
        }

        private void OnDestroy()
        {
            // すべてのコルーチンを停止
            StopAllCoroutines();
        }
    }
}
