using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LilyEnglish.Growth.UI
{
    /// <summary>パーティ一覧パネル（参考画面1枚目全体）。行を自動生成して [とじる] で閉じる。</summary>
    public class PartyListUI : MonoBehaviour
    {
        [SerializeField] private CharacterRowUI rowPrefab;
        [SerializeField] private Transform content;          // VerticalLayoutGroup 付き
        [SerializeField] private CharacterDetailUI detailPanel;
        [SerializeField] private Button closeButton;

        private readonly List<CharacterRowUI> rows = new List<CharacterRowUI>();

        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnEnable()
        {
            var gs = GrowthService.Instance;
            if (gs == null) return;
            if (rows.Count == 0) Build();
            gs.Changed += RefreshAll;
            RefreshAll();
        }

        private void OnDisable()
        {
            if (GrowthService.Instance != null) GrowthService.Instance.Changed -= RefreshAll;
        }

        private void Build()
        {
            foreach (var def in GrowthService.Instance.Party)
            {
                var row = Instantiate(rowPrefab, content);
                row.Bind(def, OpenDetail);
                rows.Add(row);
            }
        }

        private void RefreshAll()
        {
            foreach (var r in rows) r.Refresh();
        }

        private void OpenDetail(CharacterDefinition def)
        {
            if (detailPanel) detailPanel.Show(def);
        }
    }
}
