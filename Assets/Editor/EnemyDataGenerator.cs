using System.Collections.Generic;
using System.IO;
using Idle;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Image/Enemy/ のスプライトから EnemyData アセットを一括生成し、
/// EnemyDatabase にまとめて登録するエディタ拡張。
/// 既存の EnemyData は上書きせず、スプライトの再リンクのみ行うため
/// Inspectorで調整したHPやスケールは保持される。
/// </summary>
public static class EnemyDataGenerator
{
    private const string SpriteFolder = "Assets/Image/Enemy";
    private const string DataFolder = "Assets/Data/Enemies";
    private const string DatabasePath = DataFolder + "/EnemyDatabase.asset";

    /// <summary>1体分の初期設定</summary>
    private readonly struct Definition
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly EnemyCategory Category;
        public readonly int Tier;

        public Definition(string key, string displayName, EnemyCategory category, int tier)
        {
            Key = key;
            DisplayName = displayName;
            Category = category;
            Tier = tier;
        }
    }

    // 30体の定義。スプライトが存在しないものはスキップされる
    private static readonly Definition[] Definitions =
    {
        // Food
        new Definition("AppleBug",      "Apple Bug",      EnemyCategory.Food,    1),
        new Definition("EggChick",      "Egg Chick",      EnemyCategory.Food,    1),
        new Definition("DonutSlime",    "Donut Slime",    EnemyCategory.Food,    2),
        new Definition("PumpkinGhost",  "Pumpkin Ghost",  EnemyCategory.Food,    2),
        new Definition("CakeGolem",     "Cake Golem",     EnemyCategory.Food,    3),

        // Animal
        new Definition("SproutRabbit",  "Sprout Rabbit",  EnemyCategory.Animal,  1),
        new Definition("BeeBuzz",       "Bee Buzz",       EnemyCategory.Animal,  1),
        new Definition("BatWing",       "Bat Wing",       EnemyCategory.Animal,  2),
        new Definition("SpiderWeave",   "Spider Weave",   EnemyCategory.Animal,  2),
        new Definition("WolfFang",      "Wolf Fang",      EnemyCategory.Animal,  3),

        // Nature
        new Definition("Slime",         "Slime",          EnemyCategory.Nature,  1),
        new Definition("MushroomCap",   "Mushroom Cap",   EnemyCategory.Nature,  1),
        new Definition("AcornKnight",   "Acorn Knight",   EnemyCategory.Nature,  2),
        new Definition("CactusPunch",   "Cactus Punch",   EnemyCategory.Nature,  2),
        new Definition("TreantSapling", "Treant Sapling", EnemyCategory.Nature,  3),

        // Weather
        new Definition("RainDrop",      "Rain Drop",      EnemyCategory.Weather, 1),
        new Definition("SnowPuff",      "Snow Puff",      EnemyCategory.Weather, 2),
        new Definition("FireImp",       "Fire Imp",       EnemyCategory.Weather, 2),
        new Definition("ThunderBird",   "Thunder Bird",   EnemyCategory.Weather, 3),
        new Definition("StormCloud",    "Storm Cloud",    EnemyCategory.Weather, 3),

        // Object
        new Definition("BookFlap",      "Book Flap",      EnemyCategory.Object,  2),
        new Definition("MimicChest",    "Mimic Chest",    EnemyCategory.Object,  2),
        new Definition("ClockTick",     "Clock Tick",     EnemyCategory.Object,  3),
        new Definition("RuneTotem",     "Rune Totem",     EnemyCategory.Object,  3),
        new Definition("StoneGolem",    "Stone Golem",    EnemyCategory.Object,  4),

        // Dark
        new Definition("BoneRattle",    "Bone Rattle",    EnemyCategory.Dark,    3),
        new Definition("Gargoyle",      "Gargoyle",       EnemyCategory.Dark,    4),
        new Definition("DarkHelm",      "Dark Helm",      EnemyCategory.Dark,    4),
        new Definition("DragonWhelp",   "Dragon Whelp",   EnemyCategory.Dark,    5),
        new Definition("WordDevil",     "Word Devil",     EnemyCategory.Dark,    5),
    };

    // ティア別の基準HP。MoneyByClick=1 の初期状態で ★1が約8タップ になるよう設定
    private static readonly int[] BaseHpByTier = { 8, 20, 45, 90, 180 };

    // ティア別の表示倍率。上位ほど大きく見せる
    private static readonly float[] ScaleByTier = { 1.0f, 1.0f, 1.15f, 1.3f, 1.5f };

    [MenuItem("Tools/LilyClicker/敵データを生成・更新")]
    public static void Generate()
    {
        // spriteImportMode が Multiple のままだと Sprite として読み込めないため、
        // データ生成の前に必ずインポート設定を揃えておく
        EnemySpriteImporter.ApplyToAll();

        EnsureFolder("Assets/Data");
        EnsureFolder(DataFolder);

        List<EnemyData> all = new List<EnemyData>();
        int created = 0;
        int relinked = 0;
        List<string> missingSprites = new List<string>();

        foreach (Definition def in Definitions)
        {
            string spritePath = $"{SpriteFolder}/Enemy_{def.Key}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            if (sprite == null)
            {
                missingSprites.Add(def.DisplayName);
                continue;
            }

            string assetPath = $"{DataFolder}/Enemy_{def.Key}.asset";
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);

            if (data == null)
            {
                // 新規作成のときだけ初期値を書き込む
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.displayName = def.DisplayName;
                data.category = def.Category;
                data.tier = def.Tier;
                data.baseHp = BaseHpByTier[def.Tier - 1];
                data.spriteScale = ScaleByTier[def.Tier - 1];
                data.sprite = sprite;

                AssetDatabase.CreateAsset(data, assetPath);
                created++;
            }
            else if (data.sprite != sprite)
            {
                // 既存アセットは調整値を残し、スプライトだけ貼り直す
                data.sprite = sprite;
                EditorUtility.SetDirty(data);
                relinked++;
            }

            all.Add(data);
        }

        // データベース本体を作成 or 更新
        EnemyDatabase database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(DatabasePath);
        bool databaseCreated = false;
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            databaseCreated = true;
        }

        database.enemies = all;
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyDataGenerator] 完了: 登録{all.Count}体 / 新規作成{created} / スプライト再リンク{relinked} / データベース{(databaseCreated ? "新規作成" : "更新")}");

        if (missingSprites.Count > 0)
        {
            Debug.LogWarning($"[EnemyDataGenerator] スプライト未配置のためスキップ（{missingSprites.Count}体）: {string.Join(", ", missingSprites)}\n" +
                             $"{SpriteFolder}/Enemy_<キー>.png を配置してから再実行してください。");
        }

        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
