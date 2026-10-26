using System;
using System.Collections.Generic;
using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 本編ステージ1つ分の設定。背景は Assets/Resources/Backgrounds/ に置く（BackgroundSpriteImporter が Sprite 化する）。
    /// ステージを増やすときは StageCatalog.All に1行足し、背景画像（stage_06 など）を置くだけでよい。
    /// </summary>
    public sealed class StageInfo
    {
        public readonly int number;             // 1 始まり
        public readonly string displayName;
        public readonly string background;      // Resources/Backgrounds/<background>。無ければ本編の既定背景
        public readonly int recommendedLevel;   // リリィの推奨レベル
        public readonly float hpMultiplier;     // 敵HPの倍率（ボスにも掛かる）
        public readonly float goldMultiplier;   // 撃破ゴールドの倍率
        public readonly long clearExpBonus;     // クリア時のEXPボーナス（毎回）
        public readonly int minTier;            // 通常の敵が出るティアの範囲（序盤→終盤で minTier→maxTier）
        public readonly int maxTier;

        public StageInfo(int number, string displayName, string background, int recommendedLevel,
                         float hpMultiplier, float goldMultiplier, long clearExpBonus, int minTier, int maxTier)
        {
            this.number = number;
            this.displayName = displayName;
            this.background = background;
            this.recommendedLevel = recommendedLevel;
            this.hpMultiplier = hpMultiplier;
            this.goldMultiplier = goldMultiplier;
            this.clearExpBonus = clearExpBonus;
            this.minTier = minTier;
            this.maxTier = maxTier;
        }
    }

    /// <summary>
    /// ステージの一覧。番号は並び順（1 始まり）。ステージ6以降はここへ足す。
    /// 難易度の目安: 推奨レベルのリリィが 1 ステージを約 800 タップで倒せる程度（ATK×0.05 が1タップの威力）。
    /// </summary>
    public static class StageCatalog
    {
        /// <summary>ボスが出る前に倒す通常の敵の数（GameManager.targetScore の既定値と同じ）</summary>
        public const int NormalEnemyCount = 25;

        public static readonly IReadOnlyList<StageInfo> All = new[]
        {
            //               番号 名前            背景        推奨Lv HP倍率 GOLD倍率 クリアEXP 敵ティア
            new StageInfo(1, "ひだまりの森", "stage_01_battle",  1, 1.0f, 1.0f, 100, 1, 3),
            new StageInfo(2, "花咲く丘",     "stage_02",  3, 1.5f, 1.4f, 200, 1, 3),
            new StageInfo(3, "水晶の洞窟",   "stage_03",  5, 2.0f, 1.8f, 300, 1, 3),
            new StageInfo(4, "雪の峠",       "stage_04",  8, 2.5f, 2.2f, 400, 2, 3),
            new StageInfo(5, "雲の上の城",   "stage_05", 12, 3.0f, 2.6f, 500, 2, 4),
        };

        public static int Count => All.Count;

        /// <summary>範囲外なら null</summary>
        public static StageInfo Get(int number)
        {
            return number >= 1 && number <= All.Count ? All[number - 1] : null;
        }
    }

    /// <summary>
    /// ステージの進行状況（解放・クリア・最後に選んだステージ）と、いま遊んでいるステージ。
    /// PlayerPrefs の JSON に保存し、セーブデータ（data.bin）の構造には手を入れない。
    /// ステージ5をクリアしても勝手にステージ1へは戻らない（挑戦するステージは必ずプレイヤーが選ぶ）。
    /// </summary>
    public static class StageProgress
    {
        public const string SaveKey = "LilyClicker.Stage.Save.v1";

        [Serializable]
        private sealed class SaveData
        {
            public int highestUnlocked = 1;
            public int lastSelected = 1;
            public List<int> cleared = new List<int>();
        }

        private static SaveData cache;

        private static SaveData Data
        {
            get
            {
                if (cache != null) return cache;
                cache = new SaveData();
                try
                {
                    string json = PlayerPrefs.GetString(SaveKey, "");
                    if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, cache);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("StageProgress: 保存データを読めなかったため初期状態にします: " + e.Message);
                    cache = new SaveData();
                }
                if (cache.cleared == null) cache.cleared = new List<int>();
                return cache;
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        /// <summary>いま遊んでいるステージ番号。0 は拠点（ステージを遊んでいない）</summary>
        public static int Active { get; private set; }

        public static StageInfo ActiveInfo => StageCatalog.Get(Active);

        /// <summary>解放済みの最高ステージ番号（カタログの範囲内）</summary>
        public static int HighestUnlocked
        {
            get
            {
                int highest = Mathf.Max(1, Data.highestUnlocked);
                foreach (int cleared in Data.cleared) highest = Mathf.Max(highest, cleared + 1);
                return Mathf.Clamp(highest, 1, StageCatalog.Count);
            }
        }

        public static int LastSelected => Mathf.Clamp(Data.lastSelected, 1, StageCatalog.Count);

        public static bool IsUnlocked(int number) => number >= 1 && number <= HighestUnlocked;

        public static bool IsCleared(int number) => Data.cleared.Contains(number);

        public static int ClearedCount => Data.cleared.Count;

        /// <summary>敵HPの倍率。拠点にいる間（Active=0）は等倍</summary>
        public static float EnemyHpMultiplier => ActiveInfo != null ? ActiveInfo.hpMultiplier : 1f;

        public static float GoldMultiplier => ActiveInfo != null ? ActiveInfo.goldMultiplier : 1f;

        /// <summary>背景画像。無い場合は null（呼び出し側は本編の既定背景のまま）</summary>
        public static Sprite BackgroundFor(int number)
        {
            StageInfo info = StageCatalog.Get(number);
            if (info == null || string.IsNullOrEmpty(info.background)) return null;
            return Resources.Load<Sprite>("Backgrounds/" + info.background);
        }

        /// <summary>ステージを始める。解放されていないステージは始められない</summary>
        public static bool Begin(int number)
        {
            if (StageCatalog.Get(number) == null || !IsUnlocked(number)) return false;
            Active = number;
            Data.lastSelected = number;
            Save();
            return true;
        }

        /// <summary>拠点へ戻る（ステージを遊んでいない状態にする）</summary>
        public static void End()
        {
            Active = 0;
        }

        /// <summary>
        /// ステージをクリアしたことを記録し、次のステージを解放する。
        /// 初めてのクリアなら true（再挑戦のクリアは false）。ゴールド・EXP の加算はここでは行わない。
        /// </summary>
        public static bool Complete(int number)
        {
            if (StageCatalog.Get(number) == null) return false;
            bool first = !Data.cleared.Contains(number);
            if (first) Data.cleared.Add(number);
            Data.highestUnlocked = Mathf.Max(Data.highestUnlocked, Mathf.Min(number + 1, StageCatalog.Count));
            Save();
            return first;
        }

        // ─── 検証・デバッグ用 ──────────────────────────────────────
        public static string ExportJson() => JsonUtility.ToJson(Data);

        public static void ResetAll()
        {
            Active = 0;
            cache = new SaveData();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public static void UnlockAllForDebug()
        {
            Data.highestUnlocked = StageCatalog.Count;
            Save();
        }

        /// <summary>PlayerPrefs の中身を読み直す（プローブが保存を差し替えた後に使う）</summary>
        public static void Reload()
        {
            cache = null;
        }
    }
}
