using TMPro;
using UnityEngine;

namespace LilyEnglish.Growth.UI
{
    /// <summary>画面上部の「EXP / ゴールド（★） / 💎」表示。</summary>
    public class WalletUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text expText;
        [SerializeField] private TMP_Text starText;
        [SerializeField] private TMP_Text gemText;

        private void OnEnable()
        {
            if (GrowthService.Instance == null) return;
            GrowthService.Instance.Changed += Refresh;
            Refresh();
        }

        private long shownStar = -1;

        private void Update()
        {
            if (GrowthService.Instance == null) return;
            long star = GrowthService.Instance.GetBalance(Currency.Star);
            if (star != shownStar) { shownStar = star; Refresh(); }
        }

        private void OnDisable()
        {
            if (GrowthService.Instance != null) GrowthService.Instance.Changed -= Refresh;
        }

        private void Refresh()
        {
            var gs = GrowthService.Instance;
            if (expText) expText.text = gs.GetBalance(Currency.Exp).ToString("N0");
            if (starText) starText.text = gs.GetBalance(Currency.Star).ToString("N0"); // ★＝本編ゴールド
            if (gemText) gemText.text = gs.GetBalance(Currency.Gem).ToString("N0");
        }
    }
}
