using System;
using UnityEngine;

// ─── イベント種別 ────────────────────────────────────────────────
public enum QuestEventType { Villager, Chest, Enemy }

// ─── 宝箱レアリティ ──────────────────────────────────────────────
public enum ChestRarity { Normal, Miss, Rare }

// ─── バトルフェーズ ──────────────────────────────────────────────
public enum BattlePhase { MidPath, Boss }

// ─── クエスト状態 ────────────────────────────────────────────────
public enum QuestState { Idle, ShowingChoice, VillagerEvent, ChestEvent, EnemyBattle, BossBattle, QuestClear, GameOver }

// ─── クエスト内で使う単語データ（WordData の代替） ──────────────
[Serializable]
public class QuestWordData
{
    public string english;
    public string reading;
    public string meaning;

    public QuestWordData(string english, string reading, string meaning)
    {
        this.english = english;
        this.reading = reading;
        this.meaning = meaning;
    }
}

// ─── 村人1セリフ ─────────────────────────────────────────────────
[Serializable]
public class VillagerLine
{
    [TextArea(1, 3)]
    public string text;
}

// ─── 村人シナリオ ────────────────────────────────────────────────
[Serializable]
public class VillagerScenario
{
    public string speakerName = "村人";
    public VillagerLine[] lines;

    [Header("選択肢")]
    public string choiceALabel = "話を聞く";
    public string choiceBLabel = "無視する";

    [Header("選択肢Aの結果")]
    public string resultTypeA = "hp";   // "hp" / "gold" / "exp" / "trap"
    public int rewardAmountA = 30;
    public string resultMessageA = "HPが回復した！";

    [Header("選択肢Bの結果")]
    public string resultTypeB = "none"; // "none" / "trap"
    public int rewardAmountB = 0;
    public string resultMessageB = "素通りした。";
}

// ─── ボスデータ ──────────────────────────────────────────────────
[Serializable]
public class BossData
{
    public string bossName = "ダークドラゴン";
    public string bossDescription = "暗黒の力を持つ強敵！";
    public Sprite bossSprite;
    public int bossHp = 8;
    public int bossGoldReward = 500;
}

// ─── クエスト設定（ScriptableObject） ────────────────────────────
[CreateAssetMenu(fileName = "NewQuestData", menuName = "LilyClicker/Quest Data")]
public class QuestDataSO : ScriptableObject
{
    [Header("クエスト基本情報")]
    public string questName = "はじめての冒険";
    public string questDescription = "魔法の森を探検しよう！";

    [Header("道中設定")]
    public int totalSteps = 3;
    public int villagerWeight = 33;
    public int chestWeight = 33;
    public int enemyWeight = 34;

    [Header("村人シナリオ（空なら組み込みを使用）")]
    public VillagerScenario[] villagerScenarios;

    [Header("道中敵の HP 設定")]
    [Tooltip("単語文字数 × この値 = 敵 HP")]
    public int enemyHpMultiplier = 2;
    public int enemyGoldReward = 100;

    [Header("宝箱")]
    public int normalGoldMin = 50;
    public int normalGoldMax = 150;
    public int rareGoldMin = 300;
    public int rareGoldMax = 600;
    public string rareItemName = "まほうのかぎ";

    [Header("ボス設定")]
    public BossData bossData;

    [Header("クリア報酬")]
    public int clearGoldBonus = 300;
    public string clearMessage = "クエストクリア！\n見事な冒険だった！";

    [Header("プレイヤー初期HP")]
    public int playerMaxHp = 100;
}

// ─── イベント結果（ランタイム用） ────────────────────────────────
public class QuestEventResult
{
    public bool success;
    public string message;
    public int goldGained;
    public int expGained;
    public int hpChange;      // 正 = 回復、負 = ダメージ
    public string itemGained;
}

// ─── 組み込み村人シナリオ集（Inspector 未設定時のフォールバック） ─
public static class DefaultVillagerScenarios
{
    public static VillagerScenario[] Get()
    {
        return new VillagerScenario[]
        {
            new VillagerScenario
            {
                speakerName = "老人",
                lines = new VillagerLine[]
                {
                    new VillagerLine { text = "おお、旅人よ！\nこの先には危険な魔物がいるぞ。" },
                    new VillagerLine { text = "これを持っていきなさい。\n旅の役に立つはずじゃ。" }
                },
                choiceALabel = "ありがたく受け取る",
                choiceBLabel = "断って先を急ぐ",
                resultTypeA = "hp",   rewardAmountA = 30,
                resultMessageA = "老人から薬草をもらった！\nHPが30回復した！",
                resultTypeB = "none", rewardAmountB = 0,
                resultMessageB = "老人に手を振り、先へ進んだ。"
            },
            new VillagerScenario
            {
                speakerName = "商人",
                lines = new VillagerLine[]
                {
                    new VillagerLine { text = "やあ、冒険者さん！\nこの辺りで英単語の問題を解いた人がいてね。" },
                    new VillagerLine { text = "お礼に金貨をあげましょう。" }
                },
                choiceALabel = "喜んでいただく",
                choiceBLabel = "遠慮する",
                resultTypeA = "gold", rewardAmountA = 80,
                resultMessageA = "商人から金貨80Gをもらった！",
                resultTypeB = "none", rewardAmountB = 0,
                resultMessageB = "「遠慮しなくていいのに…」\n商人はため息をついた。"
            },
            new VillagerScenario
            {
                speakerName = "怪しい人物",
                lines = new VillagerLine[]
                {
                    new VillagerLine { text = "ふっふっふ…\nこの壺を買わないかね？格安だよ？" }
                },
                choiceALabel = "買ってみる（50G）",
                choiceBLabel = "無視する",
                resultTypeA = "trap", rewardAmountA = -50,
                resultMessageA = "壺を開けたら毒ガスが！\n50Gと引き換えに…ハズレだった！",
                resultTypeB = "none", rewardAmountB = 0,
                resultMessageB = "正解！そいつは詐欺師だった。\n難を逃れた！"
            },
            new VillagerScenario
            {
                speakerName = "魔法少女の師匠",
                lines = new VillagerLine[]
                {
                    new VillagerLine { text = "おやおや、リリィの仲間かい？\n英語の修行、順調そうだね。" },
                    new VillagerLine { text = "特別に回復魔法をかけてあげよう。" }
                },
                choiceALabel = "お願いします！",
                choiceBLabel = "大丈夫です",
                resultTypeA = "hp",  rewardAmountA = 50,
                resultMessageA = "師匠の回復魔法でHPが50回復した！",
                resultTypeB = "exp", rewardAmountB = 30,
                resultMessageB = "「その心意気やよし」\n精神力が高まった！EXP+30"
            }
        };
    }
}

// ─── 組み込み単語リスト ──────────────────────────────────────────
public static class QuestWordList
{
    public static QuestWordData[] GetAll()
    {
        return new QuestWordData[]
        {
            new QuestWordData("apple",      "アップル",      "りんご"),
            new QuestWordData("dragon",     "ドラゴン",      "竜"),
            new QuestWordData("magic",      "マジック",      "魔法"),
            new QuestWordData("forest",     "フォレスト",    "森"),
            new QuestWordData("sword",      "ソード",        "剣"),
            new QuestWordData("castle",     "キャッスル",    "城"),
            new QuestWordData("treasure",   "トレジャー",    "宝物"),
            new QuestWordData("monster",    "モンスター",    "怪物"),
            new QuestWordData("adventure",  "アドベンチャー", "冒険"),
            new QuestWordData("crystal",    "クリスタル",    "水晶"),
            new QuestWordData("fire",       "ファイア",      "炎"),
            new QuestWordData("water",      "ウォーター",    "水"),
            new QuestWordData("thunder",    "サンダー",      "雷"),
            new QuestWordData("shadow",     "シャドウ",      "影"),
            new QuestWordData("hero",       "ヒーロー",      "英雄"),
        };
    }

    public static QuestWordData GetRandom()
    {
        var list = GetAll();
        return list[UnityEngine.Random.Range(0, list.Length)];
    }
}
