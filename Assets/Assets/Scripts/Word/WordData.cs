using System;

namespace Idle
{
    /// <summary>
    /// 英単語データクラス
    /// CSVから読み込まれた1行分のデータを格納します
    /// </summary>
    [Serializable]
    public class WordData
    {
        public int id;
        public string english;
        public string reading;
        public string meaning;
        public int maxHp;

        public WordData()
        {
            id = 0;
            english = string.Empty;
            reading = string.Empty;
            meaning = string.Empty;
            maxHp = 0;
        }

        public WordData(int id, string english, string reading, string meaning, int maxHp)
        {
            this.id = id;
            this.english = english;
            this.reading = reading;
            this.meaning = meaning;
            this.maxHp = maxHp;
        }
    }
}
