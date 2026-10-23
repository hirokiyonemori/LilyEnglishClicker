using System;
using UnityEngine;

namespace LilyEnglish.Growth
{
    /// <summary>
    /// キャラ1人分のマスターデータ（ScriptableObject）。
    /// Project右クリック → Create → LilyEnglish → Character Definition で作成。
    /// </summary>
    [CreateAssetMenu(fileName = "Character_", menuName = "LilyEnglish/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("基本情報")]
        public string characterId = "lily";
        public string displayName = "リリー";
        public string roleName = "見習い魔法使い";
        public Sprite portrait;   // 一覧用（ドット絵 / アイコン）
        public Sprite fullBody;   // 詳細画面の立ち絵
        public Sprite tabIcon;    // 詳細画面下部のキャラ切り替えアイコン

        [Header("Lv1 の基礎ステータス")]
        public int baseHp = 120;
        public int baseAtk = 20;
        public int baseDef = 15;

        [Header("成長（Lvが上がるほど1回の上昇量が増える）")]
        public StatGrowth hpGrowth = new StatGrowth(15, 0.08f);
        public StatGrowth atkGrowth = new StatGrowth(4, 0.08f);
        public StatGrowth defGrowth = new StatGrowth(3, 0.08f);

        [Header("レベルアップ必要EXP = expBase × expGrowthRate^(Lv-1)")]
        public long expBase = 100;
        public float expGrowthRate = 1.08f;
        public int maxLevel = 200;

        [Header("固有スキル")]
        public SkillDefinition skill;

        public CharacterStats StatsAt(int level)
        {
            return new CharacterStats(
                hpGrowth.StatAt(baseHp, level),
                atkGrowth.StatAt(baseAtk, level),
                defGrowth.StatAt(baseDef, level));
        }

        public long ExpCostAt(int currentLevel)
        {
            return (long)Math.Round(expBase * Math.Pow(expGrowthRate, currentLevel - 1));
        }
    }

    [Serializable]
    public struct StatGrowth
    {
        [Tooltip("Lv1→2 の上昇量")] public int baseGain;
        [Tooltip("Lvが1上がるごとに上昇量が何倍ずつ増えるか（0.08 = 8%）")] public float gainPerLevel;

        public StatGrowth(int baseGain, float gainPerLevel)
        {
            this.baseGain = baseGain;
            this.gainPerLevel = gainPerLevel;
        }

        /// <summary>
        /// Lv の累積値（閉じた式）。上昇量 = baseGain × (1 + gainPerLevel × (l-1)) を Lv1〜(Lv-1) で合計。
        /// 差分で「次のLvで +いくつ」を出すので、表示と実際の上昇が必ず一致する。
        /// </summary>
        public int StatAt(int baseValue, int level)
        {
            int n = Mathf.Max(0, level - 1);
            double total = baseGain * (n + gainPerLevel * n * (n - 1) / 2.0);
            return baseValue + (int)Math.Round(total);
        }
    }

    [Serializable]
    public struct CharacterStats
    {
        public int hp;
        public int atk;
        public int def;

        public CharacterStats(int hp, int atk, int def)
        {
            this.hp = hp;
            this.atk = atk;
            this.def = def;
        }

        public static CharacterStats operator -(CharacterStats a, CharacterStats b)
            => new CharacterStats(a.hp - b.hp, a.atk - b.atk, a.def - b.def);
    }
}
