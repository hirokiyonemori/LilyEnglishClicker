using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>
    /// パーティ一覧の1行（参考画面1枚目）。
    /// 名前 / Lv / 必要Exp / 攻撃・防御・HP と「(+次Lvの上昇量)」/ [レベルをあげる] [特技]
    /// </summary>
    public class CharacterRowUI : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text needExpText;
        [SerializeField] private TMP_Text atkText, atkGainText;
        [SerializeField] private TMP_Text defText, defGainText;
        [SerializeField] private TMP_Text hpText, hpGainText;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button skillButton;
        [SerializeField] private LevelUpFeedback feedback; // 任意

        private CharacterDefinition def;
        private Action<CharacterDefinition> openDetail;

        public CharacterDefinition Definition => def;

        public void Bind(CharacterDefinition definition, Action<CharacterDefinition> onOpenDetail)
        {
            def = definition;
            openDetail = onOpenDetail;

            levelUpButton.onClick.RemoveAllListeners();
            levelUpButton.onClick.AddListener(OnLevelUp);
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(() => openDetail?.Invoke(def));

            if (portrait) portrait.sprite = def.portrait;
            nameText.text = def.displayName;
            Refresh();
        }

        private void OnLevelUp()
        {
            if (GrowthService.Instance.TryLevelUp(def) && feedback) feedback.Play();
        }

        public void Refresh()
        {
            if (def == null) return;
            var gs = GrowthService.Instance;
            var s = gs.GetStats(def);
            var g = gs.GetNextGain(def);
            bool max = gs.IsMaxLevel(def);

            levelText.text = $"Lv.{gs.GetLevel(def)}";
            needExpText.text = max ? "必要Exp: MAX" : $"必要Exp: {gs.GetLevelUpCost(def):N0}";

            atkText.text = s.atk.ToString("N0");
            defText.text = s.def.ToString("N0");
            hpText.text = s.hp.ToString("N0");

            atkGainText.text = max ? "" : $"(+{g.atk:N0})";
            defGainText.text = max ? "" : $"(+{g.def:N0})";
            hpGainText.text = max ? "" : $"(+{g.hp:N0})";

            levelUpButton.interactable = gs.CanLevelUp(def);
        }
    }
}
