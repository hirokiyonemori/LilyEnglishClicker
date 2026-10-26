using System;
using System.Globalization;
using Idle;

/// <summary>日次受取とGOLDを同じdata.binの一回の保存で確定。クイズは一日一回答、任意。</summary>
public sealed class DailyBonusService
{
    public const int BaseGold = 100;
    public const int QuizGold = 50;
    private readonly Data data;
    private readonly Action save;
    private readonly Func<DateTime> clock;

    public DailyBonusService(Data data, Action save, Func<DateTime> clock = null)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.clock = clock ?? (() => DateTime.Now);
    }

    public int Today => int.Parse(clock().ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    public bool CanClaimBase => Today > data.dailyBonusDay;
    public bool CanAnswer => Today > data.dailyBonusDay || (Today == data.dailyBonusDay && !data.dailyQuizAnswered);
    public bool AnsweredCorrectly => Today == data.dailyBonusDay && data.dailyQuizAnswered && data.dailyQuizCorrect;

    public DailyWordQuestion Question()
    {
        int day = Today;
        QuestWordData[] words = QuestWordList.GetAll();
        var word = words[day % words.Length];
        int correct = (day / words.Length) % 2;
        string wrong = words[(day % words.Length + 1) % words.Length].meaning;
        return new DailyWordQuestion { day = day, word = word, correctIndex = correct,
            answerA = correct == 0 ? word.meaning : wrong, answerB = correct == 1 ? word.meaning : wrong };
    }

    public bool ClaimBase(out string message)
    {
        if (!CanClaimBase) { message = "今日の通常報酬は受け取り済みです"; return false; }
        return Commit(Today, false, false, BaseGold, out message);
    }

    public bool Answer(int index, int questionDay, out string message)
    {
        if (questionDay != Today) { message = "日付が変わりました。今日の問題を開き直してください"; return false; }
        if (index < 0 || index > 1 || !CanAnswer) { message = "今日の英語ボーナスは終了しています"; return false; }
        bool correct = index == Question().correctIndex;
        int gold = (CanClaimBase ? BaseGold : 0) + (correct ? QuizGold : 0);
        if (!Commit(questionDay, true, correct, gold, out message)) return false;
        message = correct ? $"正解！ 追加 {QuizGold} GOLD を獲得！" : "正解は「" + Question().word.meaning + "」。通常報酬は受け取り済みです";
        return true;
    }

    private bool Commit(int day, bool answered, bool correct, int gold, out string message)
    {
        if (data.Money > long.MaxValue - gold) { message = "GOLDが上限に達しています"; return false; }
        long oldGold = data.Money;
        int oldDay = data.dailyBonusDay;
        bool oldAnswered = data.dailyQuizAnswered, oldCorrect = data.dailyQuizCorrect;
        data.Money += gold;
        data.dailyBonusDay = day;
        data.dailyQuizAnswered = answered;
        data.dailyQuizCorrect = correct;
        try { save(); }
        catch (Exception)
        {
            data.Money = oldGold; data.dailyBonusDay = oldDay;
            data.dailyQuizAnswered = oldAnswered; data.dailyQuizCorrect = oldCorrect;
            message = "保存できませんでした。もう一度お試しください";
            return false;
        }
        message = $"{gold} GOLD を受け取りました";
        return true;
    }
}

public sealed class DailyWordQuestion
{
    public int day, correctIndex;
    public QuestWordData word;
    public string answerA, answerB;
}
