using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Idle;
using UnityEditor;
using UnityEngine;

public static class EnemyExpansion60
{
    const string Folder = "Assets/Image/Enemy/Expansion60/";
    public static readonly string[][] Keys = {
        new[]{"AcornMouse","PetalHare","LeafHedgehog","DewSpirit","MossSnail","VioletBat"},
        new[]{"CopperHermit","StarOwl","AmberFrog","SeedTurnip","CrystalLizard","LanternCrab"},
        new[]{"HauntedTeapot","FloatingMask","MossSentinel","PineconeBeast","GlassDragonfly","MooncapMage"},
        new[]{"FrostBoar","BlueflameHorse","GearBeetle","BriarSerpent","ObsidianScorpion","SteelHarpy"},
        new[]{"CrownedTreant","BlossomMothQueen","CrystalCrabKing","SnowGolem","StormGriffin","ArcaneOwl"}
    };
    static readonly string[][] Names = {
        new[]{"どんぐりマウス","花びらうさぎ","リーフハリネズミ","しずくの精霊","苔むしスネイル","すみれコウモリ"},
        new[]{"銅殻ヤドカリ","星読みフクロウ","こはくガエル","種まきカブリン","水晶トカゲ","ランタンガニ"},
        new[]{"おばけティーポット","さまよう仮面","苔の番兵","まつぼっくり獣","ガラス羽トンボ","月キノコの魔術師"},
        new[]{"氷鎧イノシシ","蒼炎の馬","歯車コガネ","いばらヘビ","黒曜サソリ","鋼羽ハーピー"},
        new[]{"樹冠の王","花蛾の女王","水晶ガニの王","雪原ゴーレム","嵐のグリフォン","三眼の魔導フクロウ"}
    };
    [MenuItem("LilyClicker/Enemy Expansion/Install 60 and Validate")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before installing enemies.");
        var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemyEncounterBuilder.DatabasePath);
        Require(db != null && db.enemies.Count >= 30, "existing roster available");
        // Validate all sources before modifying any gameplay asset.
        for (int sheet = 0; sheet < 5; sheet++)
        {
            string path = Folder + "Sheet" + (char)('A' + sheet) + ".png";
            Require(File.Exists(path), path);
            var source = new Texture2D(2, 2);
            try { source.LoadImage(File.ReadAllBytes(path)); Require(source.GetPixels32().Any(p => p.a == 0), "real alpha " + path); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        for (int sheet = 0; sheet < 5; sheet++)
        {
            string path = Folder + "Sheet" + (char)('A' + sheet) + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false; importer.isReadable = false;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.filterMode = FilterMode.Bilinear;
            var slices = new SpriteMetaData[6];
            for (int i = 0; i < 6; i++)
            {
                int x0 = Mathf.RoundToInt((i % 3) * width / 3f), x1 = Mathf.RoundToInt((i % 3 + 1) * width / 3f);
                int y0 = (1 - i / 3) * height / 2;
                slices[i] = new SpriteMetaData { name = Keys[sheet][i], rect = new Rect(x0, y0, x1 - x0, height / 2), alignment = 0, pivot = new Vector2(.5f, .5f) };
            }
#pragma warning disable 618
            importer.spritesheet = slices;
#pragma warning restore 618
            foreach (var platform in new[]{"iPhone", "Android"})
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true; settings.maxTextureSize = 2048;
                settings.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Require(sprites.Length == 6, "six sprites " + path);
            for (int i = 0; i < 6; i++)
            {
                string dataPath = "Assets/Data/Enemies/Enemy_" + Keys[sheet][i] + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
                if (data == null) { data = ScriptableObject.CreateInstance<EnemyData>(); AssetDatabase.CreateAsset(data, dataPath); }
                data.displayName = Names[sheet][i]; data.sprite = sprites.Single(s => s.name == Keys[sheet][i]);
                data.tier = sheet + 1; data.category = sheet >= 3 ? EnemyCategory.Dark : EnemyCategory.Nature;
                data.spriteScale = sheet == 4 ? 1.35f : 1f;
                data.baseHp = new[]{8, 18, 36, 64, 210}[sheet] + new[]{-1, 0, 1, 0, 2, 1}[i] * (sheet == 4 ? 10 : sheet + 1);
                data.goldReward = 50; // Preserve existing reward economy and boss x5 multiplier.
                data.attackDamage = new[]{1, 2, 3, 5, 10}[sheet];
                data.attackInterval = sheet == 4 ? 3.5f : 0f;
                EditorUtility.SetDirty(data);
                if (!db.enemies.Contains(data)) db.enemies.Add(data);
            }
        }
        // Preserve all existing identities/GUIDs; shorter late normal fights, tougher bosses.
        foreach (var enemy in db.enemies.Where(e => !Keys.SelectMany(k => k).Contains(e.sprite.name)))
        {
            if (enemy.tier == 2) enemy.baseHp = 18;
            if (enemy.tier == 3) enemy.baseHp = 36;
            if (enemy.tier == 4) enemy.baseHp = 64;
            if (enemy.tier == 5) { enemy.baseHp = 210; enemy.attackInterval = 3.5f; }
            enemy.attackDamage = new[]{1, 2, 3, 5, 10}[enemy.tier - 1];
            EditorUtility.SetDirty(enemy);
        }
        EditorUtility.SetDirty(db);
        typeof(EnemyEncounterBuilder).GetMethod("BuildBoundsTable", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        AssetDatabase.SaveAssets();
        Validate();
    }
    [MenuItem("LilyClicker/Enemy Expansion/Validate 60")]
    public static void Validate()
    {
        var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemyEncounterBuilder.DatabasePath);
        Require(db.enemies.Count == 60 && db.enemies.Distinct().Count() == 60, "60 unique enemies");
        Require(db.enemies.All(e => e != null && e.sprite != null), "all art assigned");
        Require(db.enemies.Select(e => e.sprite).Distinct().Count() == 60, "60 unique sprites");
        var bounds = AssetDatabase.LoadAssetAtPath<EnemySpriteBounds>(EnemyEncounterBuilder.BoundsPath);
        foreach (var e in db.enemies) Require(bounds.TryGet(e.sprite, out var rect) && rect.width > .1f && rect.height > .1f, "bounds " + e.name);
        var random = UnityEngine.Random.state;
        try
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                EnemyData previous = null;
                var seen = new System.Collections.Generic.HashSet<EnemyData>();
                for (int n = 0; n < 1000; n++) { var e = db.GetRandomByTier(tier); Require(e.tier == tier && e != previous, "tier / no immediate repetition"); previous = e; seen.Add(e); }
                Require(seen.Count == db.enemies.Count(e => e.tier == tier), "every enemy reachable at tier " + tier);
            }
        }
        finally { UnityEngine.Random.state = random; }
        Require(new BossData().bossHp == 20, "adventure boss default stronger than path HP12");
        var report = new StringBuilder("name,tier,hp,damage,interval,gold,texture\n");
        foreach (var e in db.enemies) report.AppendLine($"{e.displayName},{e.tier},{e.baseHp},{(e.attackDamage > 0 ? e.attackDamage : e.tier * 2)},{(e.attackInterval > 0 ? e.attackInterval : 4)},{e.goldReward},{AssetDatabase.GetAssetPath(e.sprite.texture)}");
        var textures = db.enemies.Select(e => e.sprite.texture).Distinct().ToArray();
        long memory = textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
        long bytes = textures.Sum(t => new FileInfo(AssetDatabase.GetAssetPath(t)).Length);
        Directory.CreateDirectory("Documentation/EnemyExpansion60");
        File.WriteAllText("Documentation/EnemyExpansion60/roster.csv", report.ToString());
        File.WriteAllText("Documentation/EnemyExpansion60/capacity.txt", $"Enemies: {db.enemies.Count}\nTextures: {textures.Length}\nSource bytes: {bytes}\nEditor texture memory bytes: {memory}\nAdditional sheets bytes: {Directory.GetFiles(Folder, "*.png").Sum(f => new FileInfo(f).Length)}\n");
        Debug.Log($"[EnemyExpansion60] PASS: 60 sprites, grounding, all tiers reachable, 5000 draws without immediate repeats; memory={memory}, source={bytes}");
    }
    static void Require(bool valid, string message) { if (!valid) throw new Exception("[EnemyExpansion60 FAIL] " + message); }
}
