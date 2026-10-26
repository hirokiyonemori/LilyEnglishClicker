using System;
using System.Collections.Generic;

namespace Idle
{
    //This class is used to store the data
    [Serializable]
    public class Data
    {
        public bool isFirstStartComplete;

        public long Money;
        public long MoneyByClick;
        public long MoneyPerSecond;

        // BinaryFormatterの旧data.binでは存在しないフィールドを既定値で読み込む。
        [System.Runtime.Serialization.OptionalField] public int dailyBonusDay;
        [System.Runtime.Serialization.OptionalField] public bool dailyQuizAnswered;
        [System.Runtime.Serialization.OptionalField] public bool dailyQuizCorrect;
    }

    [Serializable]
    public class UpgradeData
    {
        public List<ItemData> cityItems = new List<ItemData>();
        public List<ItemData> residentsItems = new List<ItemData>();
        public List<ItemData> comfortItems = new List<ItemData>();
        public List<ItemData> automationItems = new List<ItemData>();

        public int comfortLvl;
        public int residentsLvl;
    }


}
