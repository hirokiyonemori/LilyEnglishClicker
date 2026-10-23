using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>
    /// 強化画面（キャラ詳細・強化ショップ）の開閉。本編下部 HUD の顔アイコンと「強化」ボタンから開く。
    /// GrowthUIBuilder（Editor）が生成する GrowthUIRoot プレハブに付く。
    /// </summary>
    public sealed class GrowthUIView : MonoBehaviour
    {
        public CharacterDetailUI detail;
        public GameObject shopRoot;
        public Button shopCloseButton;
        [Tooltip("キャラ詳細から強化ショップへ移るボタン（任意）")]
        public Button detailToShopButton;
        [Tooltip("強化ショップからキャラ詳細へ移るボタン（任意）")]
        public Button shopToDetailButton;

        private static GrowthUIView cached;

        public static GrowthUIView Find()
        {
            if (cached == null) cached = FindAnyObjectByType<GrowthUIView>(FindObjectsInactive.Include);
            return cached;
        }

        public bool IsOpen => (detail != null && detail.gameObject.activeSelf) || (shopRoot != null && shopRoot.activeSelf);

        private void Awake()
        {
            cached = this;
            CloseAll();
            if (shopCloseButton != null) shopCloseButton.onClick.AddListener(CloseAll);
            if (detailToShopButton != null) detailToShopButton.onClick.AddListener(OpenShop);
            if (shopToDetailButton != null) shopToDetailButton.onClick.AddListener(OpenDetail);
        }

        private void OnDestroy()
        {
            if (cached == this) cached = null;
        }

        private void Update()
        {
            // Android の戻るボタンで閉じる
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseAll();
        }

        public void OpenDetail()
        {
            var gs = GrowthService.Instance;
            if (gs == null || gs.MainCharacter == null || detail == null || global::QuestManager.IsQuestActive) return;
            if (shopRoot != null) shopRoot.SetActive(false);
            detail.Show(gs.MainCharacter);
        }

        public void OpenShop()
        {
            if (shopRoot == null || GrowthService.Instance == null || global::QuestManager.IsQuestActive) return;
            if (detail != null) detail.gameObject.SetActive(false);
            shopRoot.SetActive(true);
        }

        public void CloseAll()
        {
            if (detail != null) detail.gameObject.SetActive(false);
            if (shopRoot != null) shopRoot.SetActive(false);
        }
    }
}
