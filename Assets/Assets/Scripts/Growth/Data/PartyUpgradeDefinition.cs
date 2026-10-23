using System;
using System.Collections.Generic;
using UnityEngine;

namespace LilyEnglish.Growth
{
    /// <summary>
    /// パーティ全体の永続強化（参考画面の「特別な楽譜（1回だけ）」「姫のお守り（強化+N）」）。
    /// maxLevel = 1 → 1回だけ購入可能、2以上 → 何度も強化できるタイプ。
    /// </summary>
    [CreateAssetMenu(fileName = "Upgrade_", menuName = "LilyEnglish/Party Upgrade")]
    public class PartyUpgradeDefinition : ScriptableObject
    {
        public string upgradeId = "ribbon";
        public string displayName = "リリーのリボン";
        public Sprite icon;
        [TextArea] public string description = "強化するとリリィの防御力がアップする";

        [Header("価格")]
        public Currency currency = Currency.Gem;
        public long costBase = 3;
        [Tooltip("1回強化するごとの値上がり率（1 = 据え置き）")]
        public float costGrowthRate = 1.5f;
        [Tooltip("1 なら「1回だけ購入可能」")]
        public int maxLevel = 20;

        [Header("1レベルあたりの効果（複数指定可）")]
        public List<UpgradeEffect> effectsPerLevel = new List<UpgradeEffect>();

        public bool IsOneTime => maxLevel == 1;

        public long CostAt(int currentLevel)
            => (long)Math.Round(costBase * Math.Pow(costGrowthRate, currentLevel));
    }

    [Serializable]
    public class UpgradeEffect
    {
        [Tooltip("空欄 = パーティ全員（今はリリィのみ）。characterId を入れるとそのキャラだけ（例：拳の勇者だけATK UP）")]
        public string targetCharacterId = "";
        public int hpPercent;
        public int atkPercent;
        public int defPercent;

        public bool AppliesTo(string characterId)
            => string.IsNullOrEmpty(targetCharacterId) || targetCharacterId == characterId;
    }
}
