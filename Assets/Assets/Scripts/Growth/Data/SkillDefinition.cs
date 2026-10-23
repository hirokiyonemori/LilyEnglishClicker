using System;
using UnityEngine;

namespace LilyEnglish.Growth
{
    /// <summary>
    /// 固有スキル（参考画面の「会心拳 Lv22 / 攻撃力の620%」に相当）。
    /// 初期値 200% + 20%/Lv にすると Lv22 で 620% になり、参考と同じ伸び方。
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "LilyEnglish/Skill Definition")]
    public class SkillDefinition : ScriptableObject
    {
        public string skillName = "スペルスター";
        public Sprite icon;
        [TextArea] public string descriptionFormat = "冒険の攻撃で\n攻撃力の{0}%のダメージを与える";

        [Header("倍率 = base + perLevel × (Lv-1)")]
        public int baseMultiplierPercent = 200;
        public int multiplierPerLevel = 20;
        public int maxLevel = 50;

        [Header("スキルレベルアップ必要EXP = costBase × costGrowthRate^(Lv-1)")]
        public long costBase = 50;
        public float costGrowthRate = 1.15f;

        [Header("旧データ互換（本番の発動条件には使わない）")]
        [Tooltip("旧クイズ時代の設定を保持。現在は通常攻撃にスキル倍率を適用")]
        public bool requireCorrectAnswer = false;

        public int MultiplierAt(int level) => baseMultiplierPercent + multiplierPerLevel * (level - 1);

        public long CostAt(int currentLevel)
            => (long)Math.Round(costBase * Math.Pow(costGrowthRate, currentLevel - 1));

        public string DescriptionAt(int level) => string.Format(descriptionFormat, MultiplierAt(level));

        /// <summary>バトル側から呼ぶ想定のダメージ計算。</summary>
        public int CalcDamage(int atk, int level) => Mathf.RoundToInt(atk * MultiplierAt(level) / 100f);
    }
}
