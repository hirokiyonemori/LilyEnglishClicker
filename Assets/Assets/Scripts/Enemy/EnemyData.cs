using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 敵の種類（既存データの分類を保持）
    /// </summary>
    public enum EnemyCategory
    {
        Food,
        Animal,
        Nature,
        Weather,
        Object,
        Dark
    }

    /// <summary>
    /// 敵1種類分の設定データ
    /// プレハブを種類ごとに用意する代わりに、このアセットを差し替えて見た目と強さを変える
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "LilyClicker/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("表示")]
        public string displayName;
        public Sprite sprite;

        [Tooltip("プレハブ既定サイズに対する倍率。ボスは大きめにする")]
        public float spriteScale = 1f;

        [Header("分類")]
        public EnemyCategory category;

        [Range(1, 5)]
        [Tooltip("難易度ティア。1=雑魚、5=ボス")]
        public int tier = 1;

        [Header("戦闘")]
        [Tooltip("この敵の最大HP。単語には依存しない")]
        public int baseHp = 8;

        [Min(0), Tooltip("0なら従来のティア×2。個別の攻撃力を設定できる")]
        public int attackDamage;
        [Min(0), Tooltip("0ならGameManagerの既定間隔。秒単位")]
        public float attackInterval;

        [Min(0), Tooltip("撃破時のGOLD。単語や正解数には依存しない") ]
        public int goldReward = 50;

        /// <summary>
        /// 出題単語を加味した実際の最大HPを算出する
        /// WordData.maxHp（70〜350）をそのままHPにすると
        /// タップ数が跳ね上がるため、基準HPに対する倍率として扱う
        /// </summary>
        public int CalculateHp(WordData word)
        {
            if (word == null)
            {
                return Mathf.Max(1, baseHp);
            }

            // maxHp 70 → 0.85倍 / 350 → 1.25倍
            float wordModifier = 0.75f + word.maxHp / 700f;
            return Mathf.Max(1, Mathf.RoundToInt(baseHp * wordModifier));
        }
    }
}
