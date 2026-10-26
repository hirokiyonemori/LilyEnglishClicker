using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// ステージクリア画面：STAGE CLEAR! / ステージ名 / 獲得GOLD / 獲得EXP / 「拠点に戻る」「もう一度挑戦」。
    /// 表示だけを行う。GOLD・EXP は撃破・クリアの時点で保存済みなので、ここでは何も加算しない。
    /// 次のステージへは自動で進まない（拠点へ戻って、プレイヤーが選ぶ）。
    /// </summary>
    public sealed class StageResultView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private TextMeshProUGUI goldValueText;
        [SerializeField] private TextMeshProUGUI expValueText;
        [SerializeField] private TextMeshProUGUI expNoteText;
        [SerializeField] private TextMeshProUGUI unlockText;
        [SerializeField] private Button baseButton;
        [SerializeField] private Button retryButton;

        public Button BaseButton => baseButton;
        public Button RetryButton => retryButton;

        private void Awake()
        {
            if (baseButton != null) baseButton.onClick.AddListener(OnBase);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        }

        /// <param name="expTotal">このステージで得た EXP の合計（クリアボーナスを含む）</param>
        /// <param name="newStage">このクリアで新しく解放されたステージ。無ければ null</param>
        public void Show(StageInfo info, long gold, long expTotal, long clearBonus, StageInfo newStage)
        {
            gameObject.SetActive(true);
            if (titleText != null) titleText.text = "STAGE CLEAR!";
            if (stageNameText != null) stageNameText.text = $"STAGE {info.number}　{info.displayName}";
            if (goldValueText != null) goldValueText.text = $"{gold:N0} G";
            if (expValueText != null) expValueText.text = $"{expTotal:N0} EXP";
            if (expNoteText != null)
            {
                expNoteText.text = clearBonus > 0 ? $"クリアボーナス +{clearBonus:N0} を含む" : "";
                expNoteText.gameObject.SetActive(clearBonus > 0);
            }
            if (unlockText != null)
            {
                unlockText.gameObject.SetActive(newStage != null);
                if (newStage != null) unlockText.text = $"STAGE {newStage.number}「{newStage.displayName}」が解放された！";
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnBase()
        {
            if (StageDirector.Instance != null) StageDirector.Instance.ReturnToBase();
        }

        private void OnRetry()
        {
            if (StageDirector.Instance != null) StageDirector.Instance.RetryStage();
        }
    }
}
