using System;

namespace LilyEnglish.Growth
{
    public enum Currency
    {
        Exp,   // バトル・英語問題の正解で貯まる（レベルアップ / スキル強化に使う）
        Star,  // ★スター（ショップの放置販売で貯まるゴールド）
        Gem    // 💎ジェム（課金 / 広告 / ログボ）
    }

    [Serializable]
    public class Wallet
    {
        public long exp;
        public long star;
        public long gem;

        public long Get(Currency c)
        {
            switch (c)
            {
                case Currency.Exp: return exp;
                case Currency.Star: return star;
                default: return gem;
            }
        }

        public bool CanAfford(Currency c, long amount) => amount >= 0 && Get(c) >= amount;

        public bool TrySpend(Currency c, long amount)
        {
            if (!CanAfford(c, amount)) return false;
            Add(c, -amount);
            return true;
        }

        public void Add(Currency c, long amount)
        {
            switch (c)
            {
                case Currency.Exp: exp = Math.Max(0, exp + amount); break;
                case Currency.Star: star = Math.Max(0, star + amount); break;
                default: gem = Math.Max(0, gem + amount); break;
            }
        }
    }
}
