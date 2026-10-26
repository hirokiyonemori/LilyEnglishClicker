using System;
using System.Collections.Generic;
using UnityEngine;

namespace LilyEnglish.Growth
{
    /// <summary>
    /// 強化システムの中心。レベルアップ・スキル強化・パーティ強化・EXP購入・広告報酬・セーブを担当。
    /// シーンのルートに空GameObjectを置いてアタッチ（DontDestroyOnLoad）。
    ///
    /// LilyEnglishClicker への接続:
    /// ・★スター＝本編の所持ゴールド（Idle.DataManager.data.Money）。残高はそちらを読み書きする。
    /// ・EXP は本編の敵撃破（GameManager.EnemyDefeated）・クエストクリアで入る（旧クイズ用の加算APIは互換用に保持）。
    /// ・Lily（party[0]）の ATK/HP/Lv を本編のタップ攻撃・クエスト戦闘・下部 HUD が参照する。
    /// </summary>
    [DefaultExecutionOrder(-100)] // UIの OnEnable より先に Instance を用意する
    public class GrowthService : MonoBehaviour
    {
        public static GrowthService Instance { get; private set; }

        [Header("パーティ（一覧画面に上から並ぶ順）")]
        [SerializeField] private List<CharacterDefinition> party = new List<CharacterDefinition>();

        [Header("ショップの永続強化")]
        [SerializeField] private List<PartyUpgradeDefinition> upgrades = new List<PartyUpgradeDefinition>();

        [Header("ショップ：EXPパック（💎1で購入）")]
        [SerializeField] private long expPackGemCost = 1;
        [SerializeField] private long expPackBase = 500;
        [Tooltip("パーティ最高Lv × この値 が上乗せされる（参考：Lv66で約2080EXP）")]
        [SerializeField] private long expPackPerLevel = 25;

        [Header("ショップ：広告視聴でEXP")]
        [SerializeField] private int adDailyLimit = 5;
        [SerializeField] private float adExpMultiplier = 3f; // EXPパックの何倍もらえるか

        [Header("本編・クエストとの接続")]
        [Tooltip("本編で敵を1体倒すごとの EXP")]
        [SerializeField] private long expPerEnemy = 10;
        [Tooltip("クエストで英単語に正解するごとの EXP")]
        [SerializeField] private long expPerCorrectAnswer = 5;
        [Tooltip("ATK を攻撃の威力（1回のダメージ）へ換算する倍率。0.05 = ATK20 で威力1（従来の1タップ1ダメージと同じ）")]
        [SerializeField] private float attackPowerPerAtk = 0.05f;

        [Header("初期所持（新規データ時のみ）")]
        [SerializeField] private long startExp = 300;
        [SerializeField] private long startGem = 10;

        private const string SaveKey = "lily_growth_save_v1";
        private GrowthSaveData data;
#if UNITY_EDITOR
        // Editor での動作確認用（即「視聴完了」）。実機では本物の広告を SetAdService で渡すまで無効
        private IRewardedAdService adService = new DummyRewardedAdService();
#else
        private IRewardedAdService adService = new UnavailableRewardedAdService();
#endif

        /// <summary>何かが変わったら発火。UIはこれを購読して再描画する。</summary>
        public event Action Changed;
        /// <summary>レベルアップ演出用（キャラ, 新Lv）。</summary>
        public event Action<CharacterDefinition, int> LeveledUp;

        public IReadOnlyList<CharacterDefinition> Party => party;
        public IReadOnlyList<PartyUpgradeDefinition> Upgrades => upgrades;
        public Wallet Wallet => data.wallet;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            Idle.GameManager.EnemyDefeated += OnEnemyDefeated;
        }

        private bool adServiceOverridden;

        private void Start()
        {
            // 広告 SDK（AdMob）が動く環境では、本物のリワード広告を使う。
            // 動かない環境は従来どおり（エディタ＝ダミー／実機で SDK が無効＝ボタンを押せない）。SetAdService で差し替え済みならそちらを優先
            if (!adServiceOverridden && Idle.AdmobManager.instance != null && Idle.AdmobManager.instance.SdkEnabled)
                adService = new Idle.AdmobRewardedAdService();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Idle.GameManager.EnemyDefeated -= OnEnemyDefeated;
            Instance = null;
        }

        /// <summary>広告 SDK を渡す。null や破棄済みの MonoBehaviour なら広告なし扱い。</summary>
        public void SetAdService(IRewardedAdService service)
        {
            adServiceOverridden = true;
            // MonoBehaviour が実装している場合、?? では破棄済みを検知できないので Unity の == で判定する
            bool missing = service == null || (service is UnityEngine.Object o && o == null);
            adService = missing ? new UnavailableRewardedAdService() : service;
        }

        public bool IsAdAvailable => adService != null && adService.IsReady && AdRemainingToday > 0;

        // ───────── 残高（★スター＝本編ゴールド） ─────────

        /// <summary>現在の残高。★は本編の所持ゴールド（DataManager.data.Money）を返す。</summary>
        public long GetBalance(Currency c)
        {
            if (c == Currency.Star && Idle.DataManager.data != null) return Idle.DataManager.data.Money;
            return data.wallet.Get(c);
        }

        public bool CanAfford(Currency c, long amount) => amount >= 0 && GetBalance(c) >= amount;

        private bool Spend(Currency c, long amount)
        {
            if (!CanAfford(c, amount)) return false;
            Give(c, -amount);
            return true;
        }

        private void Give(Currency c, long amount)
        {
            if (c == Currency.Star && Idle.DataManager.data != null)
            {
                Idle.DataManager.data.Money = Math.Max(0, Idle.DataManager.data.Money + amount);
                Idle.DataManager.SaveData();
                return;
            }
            data.wallet.Add(c, amount);
        }

        // ───────── Lily（本編・クエスト・HUD から参照） ─────────

        /// <summary>主人公（party[0]）。未設定なら null。</summary>
        public CharacterDefinition MainCharacter => party.Count > 0 ? party[0] : null;

        public int MainLevel => MainCharacter != null ? GetLevel(MainCharacter) : 1;

        /// <summary>攻撃の威力（小数あり・最低 1）。本編のタップは端数を持ち越して使う。</summary>
        public float AttackPowerExact
        {
            get
            {
                if (MainCharacter == null) return 1f;
                return Mathf.Max(1f, GetStats(MainCharacter).atk * attackPowerPerAtk);
            }
        }

        /// <summary>攻撃の威力（本編の1タップ・クエストの1撃の基本ダメージ）。最低 1。</summary>
        public int AttackPower
        {
            get
            {
                if (MainCharacter == null) return 1;
                return Mathf.Max(1, Mathf.RoundToInt(GetStats(MainCharacter).atk * attackPowerPerAtk));
            }
        }

        /// <summary>主人公の最大HP（クエスト開始時の HP と HUD 表示）。</summary>
        public int MainMaxHp => MainCharacter != null ? Mathf.Max(1, GetStats(MainCharacter).hp) : 1;

        /// <summary>冒険の通常攻撃ダメージ（固有スキル倍率を適用）。スキル未設定なら威力そのまま。</summary>
        public int SkillDamage(int basePower)
        {
            var def = MainCharacter;
            if (def == null || def.skill == null) return Mathf.Max(1, basePower);
            return Mathf.Max(1, def.skill.CalcDamage(basePower, GetSkillLevel(def)));
        }

        // 旧クイズ/外部参照との互換口。通常戦闘はSkillDamageを使用する。
        public int CorrectAnswerDamage(int basePower) => SkillDamage(basePower);
        public void NotifyWalletChanged() => Changed?.Invoke();

        /// <summary>クエストで英単語に正解した時に呼ぶ（EXP 付与）。</summary>
        public void OnCorrectAnswer()
        {
            if (expPerCorrectAnswer > 0) AddCurrency(Currency.Exp, expPerCorrectAnswer);
        }

        private void OnEnemyDefeated()
        {
            if (expPerEnemy <= 0) return;
            AddCurrency(Currency.Exp, expPerEnemy);
            StageExpEarned += expPerEnemy;
        }

        /// <summary>ステージ開始から本編の敵撃破とクリアボーナスで得た EXP（クリア画面の「獲得EXP」用。保存はしない）</summary>
        public long StageExpEarned { get; private set; }

        public void ResetStageExp() => StageExpEarned = 0;

        /// <summary>ステージクリアのボーナス EXP を付与する（保存され、StageExpEarned にも数える）</summary>
        public void GrantStageClearExp(long amount)
        {
            if (amount <= 0) return;
            AddCurrency(Currency.Exp, amount);
            StageExpEarned += amount;
        }

        // ───────── キャラ：レベル ─────────

        public int GetLevel(CharacterDefinition def) => Progress(def).level;

        public bool IsMaxLevel(CharacterDefinition def) => Progress(def).level >= def.maxLevel;

        /// <summary>パーティ強化込みの現在ステータス。</summary>
        public CharacterStats GetStats(CharacterDefinition def) => StatsAt(def, Progress(def).level);

        /// <summary>次のLvで増える量（一覧画面の「(+728)」表示用）。</summary>
        public CharacterStats GetNextGain(CharacterDefinition def)
        {
            int lv = Progress(def).level;
            if (lv >= def.maxLevel) return default;
            return StatsAt(def, lv + 1) - StatsAt(def, lv);
        }

        public long GetLevelUpCost(CharacterDefinition def) => def.ExpCostAt(Progress(def).level);

        public bool CanLevelUp(CharacterDefinition def)
            => def != null && !IsMaxLevel(def) && CanAfford(Currency.Exp, GetLevelUpCost(def));

        public bool TryLevelUp(CharacterDefinition def)
        {
            if (!CanLevelUp(def)) return false;
            Spend(Currency.Exp, GetLevelUpCost(def));
            var p = Progress(def);
            p.level++;
            LeveledUp?.Invoke(def, p.level);
            SaveAndNotify();
            return true;
        }

        /// <summary>まとめ上げ（長押し / 「最大まで」ボタン用）。上がった回数を返す。</summary>
        public int LevelUpMax(CharacterDefinition def, int limit = 9999)
        {
            int count = 0;
            var p = Progress(def);
            while (count < limit && !IsMaxLevel(def) && Spend(Currency.Exp, GetLevelUpCost(def)))
            {
                p.level++;
                count++;
            }
            if (count > 0)
            {
                LeveledUp?.Invoke(def, p.level);
                SaveAndNotify();
            }
            return count;
        }

        // ───────── キャラ：スキル ─────────

        public int GetSkillLevel(CharacterDefinition def) => Progress(def).skillLevel;

        public bool IsSkillMax(CharacterDefinition def)
            => def.skill == null || Progress(def).skillLevel >= def.skill.maxLevel;

        public long GetSkillUpCost(CharacterDefinition def)
            => def.skill == null ? 0 : def.skill.CostAt(Progress(def).skillLevel);

        public bool CanSkillUp(CharacterDefinition def)
            => def != null && !IsSkillMax(def) && CanAfford(Currency.Exp, GetSkillUpCost(def));

        public bool TrySkillUp(CharacterDefinition def)
        {
            if (!CanSkillUp(def)) return false;
            Spend(Currency.Exp, GetSkillUpCost(def));
            Progress(def).skillLevel++;
            SaveAndNotify();
            return true;
        }

        // ───────── ショップ：パーティ強化 ─────────

        public int GetUpgradeLevel(PartyUpgradeDefinition up) => UpgradeProg(up).level;

        public bool IsUpgradeMax(PartyUpgradeDefinition up) => UpgradeProg(up).level >= up.maxLevel;

        public long GetUpgradeCost(PartyUpgradeDefinition up) => up.CostAt(UpgradeProg(up).level);

        public bool CanBuyUpgrade(PartyUpgradeDefinition up)
            => up != null && !IsUpgradeMax(up) && CanAfford(up.currency, GetUpgradeCost(up));

        public bool TryBuyUpgrade(PartyUpgradeDefinition up)
        {
            if (!CanBuyUpgrade(up)) return false;
            Spend(up.currency, GetUpgradeCost(up));
            UpgradeProg(up).level++;
            SaveAndNotify();
            return true;
        }

        // ───────── ショップ：EXP購入 / 広告 ─────────

        public long ExpPackAmount
        {
            get
            {
                int maxLv = 1;
                foreach (var def in party) maxLv = Mathf.Max(maxLv, Progress(def).level);
                return expPackBase + expPackPerLevel * maxLv;
            }
        }

        public long ExpPackGemCost => expPackGemCost;

        public bool TryBuyExpPack()
        {
            if (!Spend(Currency.Gem, expPackGemCost)) return false;
            Give(Currency.Exp, ExpPackAmount);
            SaveAndNotify();
            return true;
        }

        public int AdRemainingToday
        {
            get
            {
                ResetAdCountIfNewDay();
                return Mathf.Max(0, adDailyLimit - data.adCountToday);
            }
        }

        public int AdDailyLimit => adDailyLimit;
        public long AdExpAmount => (long)(ExpPackAmount * adExpMultiplier);

        public void WatchAdForExp(Action<bool> onDone = null)
        {
            if (!IsAdAvailable) { onDone?.Invoke(false); return; }
            adService.Show(completed =>
            {
                if (completed)
                {
                    data.adCountToday++;
                    Give(Currency.Exp, AdExpAmount);
                    SaveAndNotify();
                }
                onDone?.Invoke(completed);
            });
        }

        // ───────── 外部（バトル・英語問題）から呼ぶ ─────────

        /// <summary>バトル勝利・英語問題正解などでEXP/★/💎を付与。</summary>
        public void AddCurrency(Currency c, long amount)
        {
            Give(c, amount);
            SaveAndNotify();
        }

        // ───────── 内部 ─────────

        private CharacterStats StatsAt(CharacterDefinition def, int level)
        {
            var raw = def.StatsAt(level);
            int hpPct = 0, atkPct = 0, defPct = 0;
            foreach (var up in upgrades)
            {
                int lv = UpgradeProg(up).level;
                if (lv <= 0) continue;
                foreach (var e in up.effectsPerLevel)
                {
                    if (!e.AppliesTo(def.characterId)) continue;
                    hpPct += e.hpPercent * lv;
                    atkPct += e.atkPercent * lv;
                    defPct += e.defPercent * lv;
                }
            }
            return new CharacterStats(
                Mathf.RoundToInt(raw.hp * (100 + hpPct) / 100f),
                Mathf.RoundToInt(raw.atk * (100 + atkPct) / 100f),
                Mathf.RoundToInt(raw.def * (100 + defPct) / 100f));
        }

        private CharacterProgress Progress(CharacterDefinition def)
        {
            if (def == null) return new CharacterProgress(); // 未設定の参照でも落ちない（保存はしない）
            var p = data.characters.Find(c => c.characterId == def.characterId);
            if (p == null)
            {
                p = new CharacterProgress { characterId = def.characterId };
                data.characters.Add(p);
            }
            return p;
        }

        private UpgradeProgress UpgradeProg(PartyUpgradeDefinition up)
        {
            if (up == null) return new UpgradeProgress();
            var p = data.upgrades.Find(u => u.upgradeId == up.upgradeId);
            if (p == null)
            {
                p = new UpgradeProgress { upgradeId = up.upgradeId };
                data.upgrades.Add(p);
            }
            return p;
        }

        private void ResetAdCountIfNewDay()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (data.adDate != today)
            {
                data.adDate = today;
                data.adCountToday = 0;
            }
        }

        private void SaveAndNotify()
        {
            Save();
            Changed?.Invoke();
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<GrowthSaveData>(json);
            if (data == null)
            {
                data = new GrowthSaveData();
                data.wallet.exp = startExp;
                data.wallet.gem = startGem;
            }
        }

        [ContextMenu("セーブデータ削除（デバッグ）")]
        private void DebugReset()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            Load();
            Changed?.Invoke();
        }

        [ContextMenu("EXP +100000（デバッグ）")]
        private void DebugAddExp() => AddCurrency(Currency.Exp, 100000);
    }
}
