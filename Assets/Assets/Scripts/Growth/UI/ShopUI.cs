using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>
    /// 強化ショップ（参考画面2枚目）。
    /// 上から：パーティ強化（自動生成）→ EXPパック（💎）→ 広告でEXP（1日N回）。
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Header("パーティ強化")]
        [SerializeField] private PartyUpgradeRowUI upgradeRowPrefab;
        [SerializeField] private Transform upgradeContent;

        [Header("EXPパック")]
        [SerializeField] private TMP_Text expPackDescText;   // 「EXPが2,080手に入ります」
        [SerializeField] private TMP_Text expPackCostText;   // 「1」
        [SerializeField] private Button expPackButton;

        [Header("広告でEXP")]
        [SerializeField] private TMP_Text adDescText;
        [SerializeField] private TMP_Text adCountText;       // 「0/5」
        [SerializeField] private Button adButton;

        private readonly List<PartyUpgradeRowUI> rows = new List<PartyUpgradeRowUI>();

        private void Awake()
        {
            expPackButton.onClick.AddListener(() => GrowthService.Instance.TryBuyExpPack());
            adButton.onClick.AddListener(() => GrowthService.Instance.WatchAdForExp());
        }

        private void OnEnable()
        {
            var gs = GrowthService.Instance;
            if (gs == null) return;
            if (rows.Count == 0)
            {
                foreach (var up in gs.Upgrades)
                {
                    var row = Instantiate(upgradeRowPrefab, upgradeContent);
                    row.Bind(up);
                    rows.Add(row);
                }
            }
            gs.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (GrowthService.Instance != null) GrowthService.Instance.Changed -= Refresh;
        }

        private float nextAdCheck;

        private void Update()
        {
            // 広告は後から読み込み終わる。ショップを開いたままでも、見られるようになったらボタンを押せるようにする
            if (Time.unscaledTime < nextAdCheck) return;
            nextAdCheck = Time.unscaledTime + 0.5f;
            var gs = GrowthService.Instance;
            if (gs != null) adButton.interactable = gs.IsAdAvailable;
        }

        private void Refresh()
        {
            var gs = GrowthService.Instance;
            foreach (var r in rows) r.Refresh();

            expPackDescText.text = $"EXPが{gs.ExpPackAmount:N0}手に入ります";
            expPackCostText.text = gs.ExpPackGemCost.ToString();
            expPackButton.interactable = gs.CanAfford(Currency.Gem, gs.ExpPackGemCost);

            int used = gs.AdDailyLimit - gs.AdRemainingToday;
            adDescText.text = $"短い動画を視聴すると\nEXPが{gs.AdExpAmount:N0}手に入ります\n※1日{gs.AdDailyLimit}回まで";
            adCountText.text = $"{used}/{gs.AdDailyLimit}";
            adButton.interactable = gs.IsAdAvailable;
        }
    }
}
