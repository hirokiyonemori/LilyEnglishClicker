using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// 画面下部の Lily ステータス HUD（ポートレート・名前・レベル・HPゲージ）のコントローラー。
    /// 見た目は LilyStatusHUDBuilder（Editor）が生成する。
    ///
    /// HP は本編戦闘中は MainHealth、冒険中は QuestManager の現在値を表示する。
    /// レベル／EXP の仕組みはまだ無いので、レベルは Inspector の初期値を表示するだけにとどめる。
    /// </summary>
    public class StatusWindowController : MonoBehaviour
    {
        public enum FaceState
        {
            Idle,
            Attack,
            Damage
        }

        [Header("=== 顔アイコン設定 ===")]
        [SerializeField] private Image faceIcon;
        [SerializeField] private Sprite faceIdle;
        [SerializeField] private Sprite faceAttack;
        [SerializeField] private Sprite faceDamage;

        [Header("=== ポートレートの切り抜き ===")]
        [Tooltip("顔画像（全身絵から切り出したスプライト）を丸枠に合わせて拡大・移動する。faceIcon の親をマスク枠として扱う")]
        [SerializeField] private bool framePortrait = true;
        [Tooltip("丸枠の幅に収める範囲（元テクスチャ幅に対する割合）")]
        [SerializeField, Range(0.05f, 1f)] private float portraitCropWidth = 0.30f;
        [Tooltip("丸枠の中心に置く点（元テクスチャ全体に対する正規化座標）。表情ごとに顔の位置が違うため個別に持つ")]
        [SerializeField] private Vector2 focusIdle = new Vector2(0.49f, 0.77f);
        [SerializeField] private Vector2 focusAttack = new Vector2(0.43f, 0.75f);
        [SerializeField] private Vector2 focusDamage = new Vector2(0.50f, 0.76f);

        [Header("=== HP設定 ===")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Image hpFillImage; // スライダーを使わない場合のFillImage
        [SerializeField] private TMP_Text hpText;   // 「100 / 100」

        [Header("=== キャラクター情報 ===")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private string characterName = "LILY";
        [SerializeField, Min(1)] private int level = 1;

        [Header("=== 任意（下部HUDでは未使用。上部の GOLD/SCORE は UIManager が表示する） ===")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text scoreText;

        [Header("=== アニメーション設定 ===")]
        [SerializeField] private float faceChangeDuration = 0.3f; // 表情が戻るまでの時間

        private FaceState currentFaceState = FaceState.Idle;
        private Coroutine faceResetCoroutine;
        private int shownHp = -1;
        private int shownMaxHp = -1;

        private void Start()
        {
            // 初期状態を設定
            SetFace(FaceState.Idle);

            if (nameText != null)
            {
                nameText.text = characterName;
            }

            SetLevel(level);
            RefreshHpFromQuest();
        }

        private void Update()
        {
            RefreshHpFromQuest();
            RefreshLevelFromGrowth();
        }

        /// <summary>強化システムがあれば Lily の現在レベルを表示する（無ければ Inspector の初期値のまま）</summary>
        private void RefreshLevelFromGrowth()
        {
            var growth = LilyEnglish.Growth.GrowthService.Instance;
            if (growth == null || growth.MainCharacter == null) return;
            int lv = growth.MainLevel;
            if (lv != level) SetLevel(lv);
        }

        /// <summary>
        /// 本編と冒険の現在HPを表示する。拠点では次の戦闘の最大HPを表示する。
        /// </summary>
        private void RefreshHpFromQuest()
        {
            global::QuestManager quest = global::QuestManager.Instance;
            if (quest == null) return;

            int max;
            int current;
            if (global::QuestManager.IsQuestActive && quest.PlayerMaxHp > 0)
            {
                max = quest.PlayerMaxHp;
                current = quest.PlayerHp;
            }
            else if (Idle.Managers.Instance != null && Idle.Managers.Instance.gameManager != null
                && Idle.Managers.Instance.gameManager.stageDirector != null
                && (Idle.Managers.Instance.gameManager.stageDirector.IsBattling
                    || Idle.Managers.Instance.gameManager.stageDirector.CurrentPhase == StageDirector.Phase.Defeated))
            {
                var health = Idle.Managers.Instance.gameManager.MainHealth;
                max = health.MaxHp;
                current = health.Hp;
            }
            else
            {
                max = quest.BasePlayerMaxHp;
                current = max;
            }

            if (current == shownHp && max == shownMaxHp) return;
            SetHP(current, max);
        }

        /// <summary>
        /// 顔の表情を変更する
        /// </summary>
        /// <param name="state">表情の状態</param>
        /// <param name="autoReset">自動的にIdleに戻すかどうか</param>
        public void SetFace(FaceState state, bool autoReset = true)
        {
            if (faceIcon == null) return;

            currentFaceState = state;

            switch (state)
            {
                case FaceState.Idle:
                    if (faceIdle != null) ShowFace(faceIdle, focusIdle);
                    break;
                case FaceState.Attack:
                    if (faceAttack != null) ShowFace(faceAttack, focusAttack);
                    break;
                case FaceState.Damage:
                    if (faceDamage != null) ShowFace(faceDamage, focusDamage);
                    break;
            }

            // 自動リセットが有効で、Idle以外の場合は一定時間後にIdleに戻す
            if (autoReset && state != FaceState.Idle)
            {
                if (faceResetCoroutine != null)
                {
                    StopCoroutine(faceResetCoroutine);
                }
                faceResetCoroutine = StartCoroutine(ResetFaceAfterDelay());
            }
        }

        /// <summary>
        /// スプライトを差し替え、元テクスチャ上の focus が丸枠の中心に来るよう位置と大きさを合わせる。
        /// 3つの表情は切り出し範囲がそれぞれ違うため、スプライトの矩形ではなく元テクスチャを基準にする。
        /// </summary>
        private void ShowFace(Sprite sprite, Vector2 focus)
        {
            faceIcon.sprite = sprite;
            if (!framePortrait || sprite.texture == null) return;

            RectTransform rt = faceIcon.rectTransform;
            RectTransform frame = rt.parent as RectTransform;
            if (frame == null) return;

            float view = Mathf.Min(frame.rect.width, frame.rect.height);
            if (view <= 0f) return;

            Texture2D texture = sprite.texture;
            float unitsPerPixel = view / (portraitCropWidth * texture.width);
            Rect spriteRect = sprite.rect;
            Vector2 focusPixel = new Vector2(focus.x * texture.width, focus.y * texture.height);

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = spriteRect.size * unitsPerPixel;
            rt.anchoredPosition = (spriteRect.center - focusPixel) * unitsPerPixel;
        }

        private System.Collections.IEnumerator ResetFaceAfterDelay()
        {
            yield return new WaitForSeconds(faceChangeDuration);
            SetFace(FaceState.Idle, autoReset: false);
            faceResetCoroutine = null;
        }

        /// <summary>
        /// HPを設定する
        /// </summary>
        /// <param name="current">現在のHP</param>
        /// <param name="max">最大HP</param>
        public void SetHP(float current, float max)
        {
            float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            shownHp = Mathf.CeilToInt(current);
            shownMaxHp = Mathf.CeilToInt(max);

            if (hpSlider != null)
            {
                hpSlider.value = ratio;
            }

            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = ratio;
            }

            if (hpText != null)
            {
                hpText.text = $"{shownHp} / {shownMaxHp}";
            }
        }

        /// <summary>
        /// ゴールド表示を更新する
        /// </summary>
        public void SetGold(long gold)
        {
            if (goldText != null)
            {
                goldText.text = FormatNumber(gold);
            }
        }

        /// <summary>
        /// スコア表示を更新する
        /// </summary>
        public void SetScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = score.ToString("N0");
            }
        }

        /// <summary>
        /// レベル表示を更新する
        /// </summary>
        public void SetLevel(int level)
        {
            this.level = Mathf.Max(1, level);

            if (levelText != null)
            {
                levelText.text = $"Lv.{this.level}";
            }
        }

        /// <summary>
        /// 数値をフォーマットする（1000 → 1K, 1000000 → 1M など）
        /// </summary>
        private string FormatNumber(long number)
        {
            if (number >= 1000000000)
                return (number / 1000000000f).ToString("0.#") + "B";
            if (number >= 1000000)
                return (number / 1000000f).ToString("0.#") + "M";
            if (number >= 1000)
                return (number / 1000f).ToString("0.#") + "K";
            return number.ToString("N0");
        }

        /// <summary>
        /// 攻撃時のフィードバック（外部から呼び出し用）
        /// </summary>
        public void OnPlayerAttack()
        {
            SetFace(FaceState.Attack);
        }

        /// <summary>
        /// ダメージ時のフィードバック（外部から呼び出し用）
        /// </summary>
        public void OnPlayerDamage()
        {
            SetFace(FaceState.Damage);
        }
    }
}
