using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 敵の種類（出題される単語のカテゴリと対応させる）
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
        [Tooltip("このティアの基準HP。実HPは出題単語のmaxHpによって±25%程度変動する")]
        public int baseHp = 8;

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
