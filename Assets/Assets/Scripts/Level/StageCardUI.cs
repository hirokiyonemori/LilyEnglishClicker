using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// ステージ選択の一覧に並ぶカード1枚。背景のサムネイル・ステージ番号と名前・推奨レベル・クリア状況・挑戦ボタン。
    /// 未解放のときは鍵を出し、挑戦ボタンを隠す。見た目の組み立ては StageUIBuilder（Assets/Editor）が行う。
    /// </summary>
    public sealed class StageCardUI : MonoBehaviour
    {
        [SerializeField] private Image thumbnail;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI recommendText;
        [SerializeField] private GameObject clearBadge;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private TextMeshProUGUI lockedText;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TextMeshProUGUI challengeLabel;

        private static readonly Color Cream = new Color32(0xF5, 0xED, 0xD8, 0xFF);
        private static readonly Color Muted = new Color32(0xB8, 0xCA, 0xDA, 0xFF);
        private static readonly Color Warn = new Color32(0xF2, 0xB1, 0x6B, 0xFF);

        public int Number { get; private set; }
        public bool Unlocked { get; private set; }
        public Button ChallengeButton => challengeButton;

        /// <param name="isNew">解放されたばかりでまだクリアしていないステージ</param>
        /// <param name="lilyLevel">いまのリリィのレベル（推奨レベルに届いているかの色分け用）</param>
        public void Bind(StageInfo info, bool unlocked, bool cleared, bool isNew, int lilyLevel, Action<int> onChallenge)
        {
            Number = info.number;
            Unlocked = unlocked;

            if (numberText != null) numberText.text = $"STAGE {info.number}";
            if (nameText != null)
            {
                nameText.text = info.displayName;
                nameText.color = unlocked ? Cream : Muted;
            }

            if (thumbnail != null)
            {
                Sprite art = StageProgress.BackgroundFor(info.number);
                thumbnail.sprite = art;
                thumbnail.enabled = art != null;
            }

            if (recommendText != null)
            {
                bool underLevel = lilyLevel < info.recommendedLevel;
                recommendText.text = underLevel
                    ? $"推奨 Lv.{info.recommendedLevel}（いま Lv.{lilyLevel}）"
                    : $"推奨 Lv.{info.recommendedLevel}";
                recommendText.color = unlocked && underLevel ? Warn : Cream;
            }

            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (clearBadge != null) clearBadge.SetActive(unlocked && cleared);
            if (newBadge != null) newBadge.SetActive(unlocked && !cleared && isNew);

            if (lockedText != null)
            {
                lockedText.gameObject.SetActive(!unlocked);
                lockedText.text = info.number > 1 ? $"STAGE {info.number - 1} をクリアで解放" : "";
            }

            if (challengeButton != null)
            {
                challengeButton.gameObject.SetActive(unlocked);
                challengeButton.onClick.RemoveAllListeners();
                int number = info.number;
                challengeButton.onClick.AddListener(() => { if (onChallenge != null) onChallenge(number); });
            }
            if (challengeLabel != null) challengeLabel.text = cleared ? "再挑戦" : "挑戦";
        }
    }
}
