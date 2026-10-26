using System.Collections.Generic;
using System.IO;
using Idle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    /// <summary>1体分の初期設定</summary>
    private readonly struct Definition
    {
        public readonly string Key;
        public readonly string SpriteFileName;
        public readonly string DisplayName;
        public readonly EnemyCategory Category;
        public readonly int Tier;

        public Definition(string key, string spriteFileName, string displayName, EnemyCategory category, int tier)
        {
            Key = key;
            SpriteFileName = spriteFileName;
            DisplayName = displayName;
            Category = category;
            Tier = tier;
        }
    }

    // 敵の定義。スプライトが存在しないものはスキップされる
    private static readonly Definition[] Definitions =
    {
        new Definition("CrystalTurtle", "CrystalTurtle.png", "クリスタルタートル", EnemyCategory.Nature, 2),
        new Definition("MoonMoth", "MoonMoth.png", "ムーンモス", EnemyCategory.Nature, 1),
        new Definition("LanternFox", "LanternFox.png", "ランタンフォックス", EnemyCategory.Animal, 3),
        // Food
        new Definition("AppleBug",      "27.png",                    "Apple Bug",      EnemyCategory.Food,    1),
        new Definition("EggChick",      "17.png",                    "Egg Chick",      EnemyCategory.Food,    1),
        new Definition("DonutSlime",    "18.png",                    "Donut Slime",    EnemyCategory.Food,    2),
        new Definition("PumpkinGhost",  "Enemy_PumpkinGhost.png",    "Pumpkin Ghost",  EnemyCategory.Food,    2),
        new Definition("CakeGolem",     "8.png",                     "Cake Golem",     EnemyCategory.Food,    3),

        // Animal
        new Definition("SproutRabbit",  "11.png",                    "Sprout Rabbit",  EnemyCategory.Animal,  1),
        new Definition("BeeBuzz",       "25.png",                    "Bee Buzz",       EnemyCategory.Animal,  1),
        new Definition("BatWing",       "26.png",                    "Bat Wing",       EnemyCategory.Animal,  2),
        new Definition("SpiderWeave",   "12.png",                    "Spider Weave",   EnemyCategory.Animal,  2),
        new Definition("WolfFang",      "2.png",                     "Wolf Fang",      EnemyCategory.Animal,  3),

        // Nature
        new Definition("Slime",         "Enemy_Slime.png",           "Slime",          EnemyCategory.Nature,  1),
        new Definition("MushroomCap",   "15.png",                    "Mushroom Cap",   EnemyCategory.Nature,  1),
        new Definition("AcornKnight",   "21.png",                    "Acorn Knight",   EnemyCategory.Nature,  2),
        new Definition("CactusPunch",   "19.png",                    "Cactus Punch",   EnemyCategory.Nature,  2),
        new Definition("TreantSapling", "3.png",                     "Treant Sapling", EnemyCategory.Nature,  3),

        // Weather
        new Definition("RainDrop",      "14.png",                    "Rain Drop",      EnemyCategory.Weather, 1),
        new Definition("SnowPuff",      "13.png",                    "Snow Puff",      EnemyCategory.Weather, 2),
        new Definition("FireImp",       "6.png",                     "Fire Imp",       EnemyCategory.Weather, 2),
        new Definition("ThunderBird",   "1.png",                     "Thunder Bird",   EnemyCategory.Weather, 3),
        new Definition("StormCloud",    "4.png",                     "Storm Cloud",    EnemyCategory.Weather, 3),

        // Object
        new Definition("BookFlap",      "20.png",                    "Book Flap",      EnemyCategory.Object,  2),
        new Definition("MimicChest",    "16.png",                    "Mimic Chest",    EnemyCategory.Object,  2),
        new Definition("ClockTick",     "7.png",                     "Clock Tick",     EnemyCategory.Object,  3),
        new Definition("RuneTotem",     "5.png",                     "Rune Totem",     EnemyCategory.Object,  3),
        new Definition("StoneGolem",    "Enemy_StoneGolem.png",      "Stone Golem",    EnemyCategory.Object,  4),

        // Dark
        new Definition("BoneRattle",    "9.png",                     "Bone Rattle",    EnemyCategory.Dark,    3),
        new Definition("Gargoyle",      "22.png",                    "Gargoyle",       EnemyCategory.Dark,    4),
        new Definition("DarkHelm",      "24.png",                    "Dark Helm",      EnemyCategory.Dark,    4),
        new Definition("DragonWhelp",   "23.png",                    "Dragon Whelp",   EnemyCategory.Dark,    5),
        new Definition("WordDevil",     "10.png",                    "Word Devil",     EnemyCategory.Dark,    5),
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
            string spritePath = $"{SpriteFolder}/{def.SpriteFileName}";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            if (sprite == null)
            {
                missingSprites.Add($"{def.DisplayName} ({def.SpriteFileName})");
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
            Debug.LogWarning($"[EnemyDataGenerator] スプライト未配置のためスキップ（{missingSprites.Count}体）: {string.Join(", ", missingSprites)}");
        }

        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }

    [MenuItem("Tools/LilyClicker/敵データ接続を復旧")]
    public static void Repair()
    {
        Generate();

        EnemyDatabase database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(DatabasePath);
        if (database == null)
        {
            throw new FileNotFoundException("EnemyDatabase could not be loaded after generation.", DatabasePath);
        }

        int managerCount;
        int changedCount = AssignDatabaseToGameScene(database, out managerCount);
        Debug.Log($"[EnemyDataGenerator] Game scene: GameManager {managerCount}件 / EnemyDatabase割り当て更新 {changedCount}件");
    }

    private static int AssignDatabaseToGameScene(EnemyDatabase database, out int managerCount)
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        int changedCount = 0;
        managerCount = 0;

        try
        {
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Idle.GameManager manager in root.GetComponentsInChildren<Idle.GameManager>(true))
                {
                    managerCount++;
                    if (manager.enemyDatabase == database)
                    {
                        continue;
                    }

                    Undo.RecordObject(manager, "Assign Enemy Database");
                    manager.enemyDatabase = database;
                    EditorUtility.SetDirty(manager);
                    changedCount++;
                }
            }

            if (managerCount == 0)
            {
                throw new MissingComponentException($"GameManager was not found in {GameScenePath}.");
            }

            if (changedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        return changedCount;
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
