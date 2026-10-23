using System;
using System.Collections.Generic;

namespace LilyEnglish.Growth
{
    [Serializable]
    public class CharacterProgress
    {
        public string characterId;
        public int level = 1;
        public int skillLevel = 1;
    }

    [Serializable]
    public class UpgradeProgress
    {
        public string upgradeId;
        public int level; // 0 = 未購入
    }

    [Serializable]
    public class GrowthSaveData
    {
        public Wallet wallet = new Wallet();
        public List<CharacterProgress> characters = new List<CharacterProgress>();
        public List<UpgradeProgress> upgrades = new List<UpgradeProgress>();

        // 広告視聴の1日上限管理
        public string adDate = "";
        public int adCountToday;
    }
}
