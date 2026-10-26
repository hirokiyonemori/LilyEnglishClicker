using System.Collections.Generic;
using LilyEnglish.Growth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// 拠点画面：ステージを縦スクロールの一覧から選ぶ。上にゴールド・EXP・リリィのレベル、下に強化へのボタン。
    /// 一覧はカタログ（StageCatalog）の件数ぶん自動で作るので、ステージを足してもこの画面の作り直しは要らない。
    /// 強化画面（GrowthUIView）は別の Canvas なので、拠点を開いたままでも開閉できる。
    /// </summary>
    public sealed class StageSelectView : MonoBehaviour
    {
        [SerializeField] private Image backdrop;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private StageCardUI cardPrefab;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI expText;
        [SerializeField] private TextMeshProUGUI levelText;

        private readonly List<StageCardUI> cards = new List<StageCardUI>();
        private float nextWalletRefresh;
        private int shownLevel = -1;

        public IReadOnlyList<StageCardUI> Cards => cards;
        public ScrollRect Scroll => scroll;

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
            ScrollToFrontier();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>カードの内容（解放・クリア・推奨レベルの色）を最新に更新する</summary>
        public void Refresh()
        {
            EnsureCards();
            var growth = GrowthService.Instance;
            int lilyLevel = growth != null ? growth.MainLevel : 1;
            shownLevel = lilyLevel;
            int frontier = StageProgress.HighestUnlocked;
            for (int i = 0; i < cards.Count; i++)
            {
                StageInfo info = StageCatalog.Get(i + 1);
                bool unlocked = StageProgress.IsUnlocked(info.number);
                bool cleared = StageProgress.IsCleared(info.number);
                bool isNew = info.number > 1 && info.number == frontier;
                cards[i].Bind(info, unlocked, cleared, isNew, lilyLevel, OnChallenge);
            }
            RefreshWallet();
            if (backdrop != null)
            {
                // 背景は、いちばん先まで解放したステージの絵（次の冒険先の気分）
                Sprite art = StageProgress.BackgroundFor(frontier);
                if (art != null) backdrop.sprite = art;
            }
        }

        private void EnsureCards()
        {
            if (cardPrefab == null || content == null) return;
            while (cards.Count < StageCatalog.Count)
            {
                StageCardUI card = Instantiate(cardPrefab, content);
                card.gameObject.name = $"StageCard_{cards.Count + 1:00}";
                card.gameObject.SetActive(true);
                cards.Add(card);
            }
            while (cards.Count > StageCatalog.Count)
            {
                Destroy(cards[cards.Count - 1].gameObject);
                cards.RemoveAt(cards.Count - 1);
            }
        }

        /// <summary>解放済みの最先端のステージが画面の中央に来るようにスクロールする</summary>
        public void ScrollToFrontier()
        {
            if (scroll == null || content == null || cards.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            int index = Mathf.Clamp(StageProgress.HighestUnlocked, 1, cards.Count) - 1;
            RectTransform card = (RectTransform)cards[index].transform;
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;

            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;
            float fromTop = -content.InverseTransformPoint(centerWorld).y; // content は上端基準（pivot y=1）
            float maxScroll = Mathf.Max(0f, content.rect.height - viewport.rect.height);
            float target = Mathf.Clamp(fromTop - viewport.rect.height * 0.5f, 0f, maxScroll);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, target);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextWalletRefresh) return;
            nextWalletRefresh = Time.unscaledTime + 0.25f;
            // 強化画面でレベルが上がったら、カードの推奨レベルの色分けも更新する
            var growth = GrowthService.Instance;
            if (growth != null && growth.MainLevel != shownLevel) Refresh();
            else RefreshWallet();
        }

        private void RefreshWallet()
        {
            var growth = GrowthService.Instance;
            long gold = DataManager.data != null ? DataManager.data.Money : 0;
            if (goldText != null) goldText.text = gold.ToString("N0");
            if (expText != null) expText.text = growth != null ? growth.GetBalance(Currency.Exp).ToString("N0") : "0";
            if (levelText != null) levelText.text = growth != null ? $"リリィ Lv.{growth.MainLevel}" : "リリィ";
        }

        private void OnChallenge(int number)
        {
            if (StageDirector.Instance != null) StageDirector.Instance.StartStage(number);
        }
    }
}
