using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// ドラクエ風ステータスウィンドウのコントローラー
    /// 画面下部に表示し、キャラクターの顔アイコン・HP・ゴールド・スコアを管理
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

        [Header("=== HP設定 ===")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Image hpFillImage; // スライダーを使わない場合のFillImage

        [Header("=== ステータス表示（アタッチ先の名前に対応） ===")]
        [SerializeField] private Text nameText_HP;      // NameText にアタッチ
        [SerializeField] private Text nameText1_Gold;   // NameText (1) にアタッチ
        [SerializeField] private Text nameText2_Score;  // NameText (2) にアタッチ
        [SerializeField] private Text nameText3_Level;  // NameText (3) にアタッチ
        [SerializeField] private Text nameText4_Name;   // NameText (4) にアタッチ

        [Header("=== アニメーション設定 ===")]
        [SerializeField] private float faceChangeDuration = 0.3f; // 表情が戻るまでの時間

        private FaceState currentFaceState = FaceState.Idle;
        private Coroutine faceResetCoroutine;

        private void Start()
        {
            // 初期状態を設定
            SetFace(FaceState.Idle);
            
            if (nameText4_Name != null)
            {
                nameText4_Name.text = "Lily";
            }
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
                    if (faceIdle != null) faceIcon.sprite = faceIdle;
                    break;
                case FaceState.Attack:
                    if (faceAttack != null) faceIcon.sprite = faceAttack;
                    break;
                case FaceState.Damage:
                    if (faceDamage != null) faceIcon.sprite = faceDamage;
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
            float ratio = max > 0 ? current / max : 0f;

            if (hpSlider != null)
            {
                hpSlider.value = ratio;
            }

            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = ratio;
            }

            if (nameText_HP != null)
            {
                nameText_HP.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        /// <summary>
        /// ゴールド表示を更新する
        /// </summary>
        public void SetGold(long gold)
        {
            if (nameText1_Gold != null)
            {
                nameText1_Gold.text = FormatNumber(gold);
            }
        }

        /// <summary>
        /// スコア表示を更新する
        /// </summary>
        public void SetScore(int score)
        {
            if (nameText2_Score != null)
            {
                nameText2_Score.text = score.ToString("N0");
            }
        }

        /// <summary>
        /// レベル表示を更新する
        /// </summary>
        public void SetLevel(int level)
        {
            if (nameText3_Level != null)
            {
                nameText3_Level.text = $"Lv.{level}";
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
