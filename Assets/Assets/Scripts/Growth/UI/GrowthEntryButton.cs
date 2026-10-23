using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>
    /// 本編 HUD から強化画面を開くボタン（顔アイコン＝キャラ詳細、「強化」ボタン＝強化ショップ）。
    /// 強化できるもの（レベルアップ・スキル・ショップの強化）があれば「!」バッジを出す。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class GrowthEntryButton : MonoBehaviour
    {
        public enum Target { CharacterDetail, Shop }

        public Target target = Target.Shop;
        [Tooltip("強化できるものがある時に出す印（任意）")]
        public GameObject affordableBadge;

        private Button button;
        private float nextCheck;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Open);
            if (affordableBadge != null) affordableBadge.SetActive(false);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Open);
        }

        private void Open()
        {
            GrowthUIView view = GrowthUIView.Find();
            if (view == null)
            {
                Debug.LogError("[GrowthEntryButton] GrowthUIView がシーンにありません。LilyClicker/強化システム メニューで再構築してください。");
                return;
            }
            if (target == Target.CharacterDetail) view.OpenDetail();
            else view.OpenShop();
        }

        private void Update()
        {
            if (affordableBadge == null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            affordableBadge.SetActive(HasSomethingToBuy());
        }

        private bool HasSomethingToBuy()
        {
            var gs = GrowthService.Instance;
            if (gs == null) return false;
            var lily = gs.MainCharacter;
            if (target == Target.CharacterDetail)
                return lily != null && (gs.CanLevelUp(lily) || gs.CanSkillUp(lily));
            foreach (var up in gs.Upgrades)
                if (gs.CanBuyUpgrade(up)) return true;
            return false;
        }
    }
}
