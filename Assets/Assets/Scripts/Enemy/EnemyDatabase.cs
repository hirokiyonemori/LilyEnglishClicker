using System.Collections.Generic;
using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 全種類の敵データをまとめて保持し、進行度に応じた抽選を行う
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDatabase", menuName = "LilyClicker/Enemy Database")]
    public class EnemyDatabase : ScriptableObject
    {
        [Tooltip("出現しうる敵の全種類")]
        public List<EnemyData> enemies = new List<EnemyData>();

        // 抽選のたびにListを新規確保しないよう使い回す
        private readonly List<EnemyData> candidateBuffer = new List<EnemyData>();

        /// <summary>
        /// 指定ティアの敵をランダムに1体返す。
        /// 該当ティアが1体も登録されていない場合は全体からランダムに返す
        /// </summary>
        public EnemyData GetRandomByTier(int tier)
        {
            if (enemies.Count == 0)
            {
                Debug.LogWarning("EnemyDatabase: 敵が1体も登録されていません。");
                return null;
            }

            candidateBuffer.Clear();
            foreach (EnemyData enemy in enemies)
            {
                if (enemy != null && enemy.tier == tier)
                {
                    candidateBuffer.Add(enemy);
                }
            }

            if (candidateBuffer.Count == 0)
            {
                Debug.LogWarning($"EnemyDatabase: ティア{tier}の敵が登録されていません。全体から抽選します。");
                return enemies[Random.Range(0, enemies.Count)];
            }

            return candidateBuffer[Random.Range(0, candidateBuffer.Count)];
        }

        /// <summary>
        /// ステージの進行度に応じた敵を1体返す。
        /// 序盤はティア1、終盤に向けて上がっていき、最後の1体はティア5（ボス）になる
        /// </summary>
        /// <param name="defeatedCount">これまでに倒した数</param>
        /// <param name="targetCount">クリアに必要な数</param>
        public EnemyData GetRandomForProgress(int defeatedCount, int targetCount)
        {
            if (targetCount <= 0)
            {
                return GetRandomByTier(1);
            }

            // ステージ最後の1体はボス
            if (defeatedCount >= targetCount - 1)
            {
                return GetRandomByTier(5);
            }

            float progress = Mathf.Clamp01((float)defeatedCount / targetCount);
            int tier = Mathf.Clamp(1 + Mathf.FloorToInt(progress * 4f), 1, 4);
            return GetRandomByTier(tier);
        }
    }
}
