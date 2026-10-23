using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>
    /// キャラ詳細（参考画面3枚目）。立ち絵 / ステータス / [EXP レベルアップ] /
    /// スキル名・Lv・説明 / [EXP スキルレベルアップ] / 下部のキャラ切り替えタブ。
    /// </summary>
    public class CharacterDetailUI : MonoBehaviour
    {
        [Header("キャラ")]
        [SerializeField] private Image fullBodyImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text hpText, atkText, defText;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpCostText;

        [Header("スキル")]
        [SerializeField] private Image skillIcon;
        [SerializeField] private TMP_Text skillNameText;
        [SerializeField] private TMP_Text skillLevelText;
        [SerializeField] private TMP_Text skillDescText;
        [SerializeField] private Button skillUpButton;
        [SerializeField] private TMP_Text skillUpCostText;

        [Header("下部タブ")]
        [SerializeField] private Button tabPrefab;  // 子に Image を持つボタン
        [SerializeField] private Transform tabContainer;
        [SerializeField] private Button closeButton;

        [SerializeField] private LevelUpFeedback feedback; // 任意

        private CharacterDefinition current;
        private readonly List<Button> tabs = new List<Button>();

        private void Awake()
        {
            levelUpButton.onClick.AddListener(() =>
            {
                if (GrowthService.Instance.TryLevelUp(current) && feedback) feedback.Play();
            });
            skillUpButton.onClick.AddListener(() =>
            {
                if (GrowthService.Instance.TrySkillUp(current) && feedback) feedback.Play();
            });
            if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnEnable()
        {
            if (GrowthService.Instance != null) GrowthService.Instance.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (GrowthService.Instance != null) GrowthService.Instance.Changed -= Refresh;
        }

        public void Show(CharacterDefinition def)
        {
            current = def;
            gameObject.SetActive(true);
            if (tabs.Count == 0) BuildTabs();
            Refresh();
        }

        private void BuildTabs()
        {
            if (tabPrefab == null || tabContainer == null) return;
            foreach (var def in GrowthService.Instance.Party)
            {
                var d = def;
                var tab = Instantiate(tabPrefab, tabContainer);
                // [0] はボタン自身の背景なので、子の Image（顔アイコン）を使う
                var imgs = tab.GetComponentsInChildren<Image>();
                var img = imgs.Length > 1 ? imgs[1] : imgs[0];
                if (img && d.tabIcon) img.sprite = d.tabIcon;
                tab.onClick.AddListener(() => Show(d));
                tabs.Add(tab);
            }
        }

        private void Refresh()
        {
            if (current == null) return;
            var gs = GrowthService.Instance;
            var s = gs.GetStats(current);

            if (fullBodyImage) fullBodyImage.sprite = current.fullBody;
            nameText.text = current.displayName;
            levelText.text = $"Lv{gs.GetLevel(current)}";
            hpText.text = s.hp.ToString("N0");
            atkText.text = s.atk.ToString("N0");
            defText.text = s.def.ToString("N0");

            bool lvMax = gs.IsMaxLevel(current);
            levelUpCostText.text = lvMax ? "MAX" : $"{gs.GetLevelUpCost(current):N0} EXP";
            levelUpButton.interactable = gs.CanLevelUp(current);

            var skill = current.skill;
            bool hasSkill = skill != null;
            if (skillIcon) { skillIcon.enabled = hasSkill; if (hasSkill) skillIcon.sprite = skill.icon; }
            skillNameText.text = hasSkill ? skill.skillName : "-";
            skillLevelText.text = hasSkill ? $"Lv{gs.GetSkillLevel(current)}" : "";
            skillDescText.text = hasSkill ? skill.DescriptionAt(gs.GetSkillLevel(current)) : "";

            bool skMax = gs.IsSkillMax(current);
            skillUpCostText.text = !hasSkill ? "" : skMax ? "MAX" : $"{gs.GetSkillUpCost(current):N0} EXP";
            skillUpButton.interactable = gs.CanSkillUp(current);
        }
    }
}
