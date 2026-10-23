using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>ショップの強化1行（「特別な楽譜」「姫のお守り」タイプ）。</summary>
    public class PartyUpgradeRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text badgeText;    // 「1回だけ購入可能」or「強化+3」
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text buttonLabel;  // 「購入する」「強化する」「購入済み」
        [SerializeField] private Image currencyIcon;
        [SerializeField] private Sprite gemSprite, starSprite, expSprite;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject fallbackIcon; // 任意：icon 未設定時に出す代わりの絵

        private PartyUpgradeDefinition up;

        public void Bind(PartyUpgradeDefinition definition)
        {
            up = definition;
            if (icon)
            {
                // アイコン未設定なら白い四角を出さず、行に置いた代わりの紋章（fallbackIcon）を見せる
                icon.sprite = up.icon;
                icon.enabled = up.icon != null;
                if (fallbackIcon) fallbackIcon.SetActive(up.icon == null);
            }
            nameText.text = up.displayName;
            descText.text = up.description;
            if (currencyIcon)
            {
                currencyIcon.sprite = up.currency == Currency.Gem ? gemSprite // ★＝本編ゴールド（コインの絵）
                    : up.currency == Currency.Star ? starSprite : expSprite;
            }
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => GrowthService.Instance.TryBuyUpgrade(up));
            Refresh();
        }

        public void Refresh()
        {
            if (up == null) return;
            var gs = GrowthService.Instance;
            int lv = gs.GetUpgradeLevel(up);
            bool max = gs.IsUpgradeMax(up);

            if (badgeText) badgeText.text = up.IsOneTime ? "1回だけ購入可能" : $"強化+{lv}";
            costText.text = max ? "" : gs.GetUpgradeCost(up).ToString("N0");
            if (currencyIcon) currencyIcon.enabled = !max;
            if (buttonLabel)
                buttonLabel.text = max ? (up.IsOneTime ? "購入済み" : "MAX") : (up.IsOneTime ? "購入する" : "強化する");
            buyButton.interactable = gs.CanBuyUpgrade(up);
        }
    }
}
