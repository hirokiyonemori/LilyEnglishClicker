using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Idle
{
    /// <summary>
    /// リザルト画面を制御するマネージャー
    /// 25問クリア時に表示され、プレイヤーを褒めて次への期待を持たせる
    /// </summary>
    public class ResultManager : MonoBehaviour
    {
        [Header("UI パーツ")]
        public GameObject resultCanvas; // リザルト画面全体
        public Image lilyImage; // リリィの喜び立ち絵
        public Sprite lilyHappySprite; // リリィの喜び表情スプライト

        [Header("テキスト")]
        public Text titleText; // 「ステージクリア！」
        public Text goldText; // 獲得ゴールド表示
        public Text wordCountText; // マスター単語数表示
        public Text rewardText; // 新魔法習得など

        [Header("ボタン")]
        public Button titleButton; // タイトルへ戻るボタン
        public Button shareButton; // SNSシェアボタン（オプション）

        [Header("演出設定")]
        public float textAnimationDelay = 0.5f; // テキスト表示の遅延
        public float goldCountDuration = 2.0f; // ゴールドカウントアップの時間

        [Header("サウンド")]
        public AudioClip resultBGM; // リザルト専用BGM（オプション）
        public AudioClip successSound; // クリア成功SE

        private bool isShowing = false;

        private void Start()
        {
            // 最初は非表示
            if (resultCanvas != null)
            {
                resultCanvas.SetActive(false);
            }

            // ボタンイベント登録
            if (titleButton != null)
            {
                titleButton.onClick.AddListener(OnTitleButtonClicked);
            }

            if (shareButton != null)
            {
                shareButton.onClick.AddListener(OnShareButtonClicked);
            }
        }

        /// <summary>
        /// リザルト画面を表示
        /// </summary>
        /// <param name="totalGold">獲得した総ゴールド</param>
        /// <param name="clearedCount">倒した敵の数</param>
        /// <param name="totalWords">総単語数（通常25）</param>
        public void ShowResult(long totalGold, int clearedCount, int totalWords = 25)
        {
            if (isShowing) return; // 二重表示防止
            isShowing = true;

            // リザルト画面を表示
            if (resultCanvas != null)
            {
                resultCanvas.SetActive(true);
            }

            // リリィの立ち絵を喜び表情に
            if (lilyImage != null && lilyHappySprite != null)
            {
                lilyImage.sprite = lilyHappySprite;
            }

            // 成功SE再生
            if (successSound != null && Managers.Instance != null && Managers.Instance.audioManager != null)
            {
                Managers.Instance.audioManager.PlaySound(successSound);
            }

            // アニメーション付きで結果を表示
            StartCoroutine(ShowResultAnimation(totalGold, clearedCount, totalWords));
        }

        /// <summary>
        /// リザルト表示アニメーション
        /// </summary>
        private IEnumerator ShowResultAnimation(long totalGold, int clearedCount, int totalWords)
        {
            // タイトル表示
            if (titleText != null)
            {
                titleText.text = clearedCount >= totalWords ? "ステージクリア！" : "よく頑張りました！";
                titleText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(textAnimationDelay);

            // ゴールド表示（カウントアップアニメーション）
            if (goldText != null)
            {
                goldText.gameObject.SetActive(true);
                yield return StartCoroutine(CountUpGold(totalGold));
            }

            yield return new WaitForSeconds(textAnimationDelay * 0.5f);

            // 単語数表示
            if (wordCountText != null)
            {
                wordCountText.text = $"マスター単語：{clearedCount} / {totalWords}";
                wordCountText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(textAnimationDelay);

            // ご褒美テキスト表示（完全クリア時のみ）
            if (rewardText != null && clearedCount >= totalWords)
            {
                rewardText.text = "新魔法『フライ』を習得！\n次のステージで使えるよ！";
                rewardText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(textAnimationDelay);

            // ボタンを有効化
            if (titleButton != null)
            {
                titleButton.gameObject.SetActive(true);
            }

            if (shareButton != null)
            {
                shareButton.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// ゴールドをカウントアップ表示するアニメーション
        /// </summary>
        private IEnumerator CountUpGold(long targetGold)
        {
            float elapsed = 0f;
            long currentGold = 0;

            while (elapsed < goldCountDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / goldCountDuration;

                // イージング（最初速く、後で遅く）
                progress = 1f - Mathf.Pow(1f - progress, 3f);

                currentGold = (long)(targetGold * progress);
                goldText.text = $"獲得ゴールド：{currentGold.ToString("N0")} G";

                yield return null;
            }

            // 最終値を正確に表示
            goldText.text = $"獲得ゴールド：{targetGold.ToString("N0")} G";
        }

        /// <summary>
        /// タイトルボタンクリック時
        /// </summary>
        private void OnTitleButtonClicked()
        {
            // タイトルシーンに戻る（シーン名は要確認）
            // 現在のシーンを再読み込みする場合
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

            // または特定のタイトルシーンに遷移
            // SceneManager.LoadScene("TitleScene");
        }

        /// <summary>
        /// シェアボタンクリック時
        /// </summary>
        private void OnShareButtonClicked()
        {
            ShareToTwitter();
        }

        /// <summary>
        /// Twitterにシェア
        /// </summary>
        private void ShareToTwitter()
        {
            // シェアするメッセージ
            string message = "魔法少女リリィと一緒に英単語をマスターしたよ！ #リリィの英単語冒険";

            // ゲームのURL（実際のURLに置き換える）
            string url = "https://yourgame.example.com/";

            // TwitterのWeb Intent URLを開く
            string twitterUrl = $"https://twitter.com/intent/tweet?text={UnityEngine.Networking.UnityWebRequest.EscapeURL(message)}&url={UnityEngine.Networking.UnityWebRequest.EscapeURL(url)}";

            Application.OpenURL(twitterUrl);
        }

        /// <summary>
        /// リザルト画面を閉じる
        /// </summary>
        public void HideResult()
        {
            if (resultCanvas != null)
            {
                resultCanvas.SetActive(false);
            }

            isShowing = false;
        }

        private void OnDestroy()
        {
            // ボタンイベントの解除
            if (titleButton != null)
            {
                titleButton.onClick.RemoveListener(OnTitleButtonClicked);
            }

            if (shareButton != null)
            {
                shareButton.onClick.RemoveListener(OnShareButtonClicked);
            }

            StopAllCoroutines();
        }
    }
}
