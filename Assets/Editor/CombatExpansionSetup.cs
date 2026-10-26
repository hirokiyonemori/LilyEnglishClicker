using System;
using System.Linq;
using System.Reflection;
using Idle;
using UnityEditor;
using UnityEngine;

public static class CombatExpansionSetup
{
    [MenuItem("LilyClicker/Combat Expansion/Install and Validate")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before installing");
        var database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemyEncounterBuilder.DatabasePath);
        string[] keys = { "CrystalTurtle", "MoonMoth", "LanternFox" };
        string[] names = { "クリスタルタートル", "ムーンモス", "ランタンフォックス" };
        int[] tiers = { 2, 1, 3 }, hp = { 20, 8, 45 };
        for (int i = 0; i < keys.Length; i++)
        {
            string path = "Assets/Data/Enemies/Enemy_" + keys[i] + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.displayName = names[i]; data.tier = tiers[i]; data.baseHp = hp[i];
                data.category = i == 2 ? EnemyCategory.Animal : EnemyCategory.Nature;
                data.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/Enemy/" + keys[i] + ".png");
                AssetDatabase.CreateAsset(data, path);
            }
            if (!database.enemies.Contains(data)) database.enemies.Add(data);
        }
        EditorUtility.SetDirty(database);
        typeof(EnemyEncounterBuilder).GetMethod("BuildBoundsTable", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        AssetDatabase.SaveAssets();
        var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemyEncounterBuilder.DatabasePath);
        Require(db.enemies.Count >= 30, "existing and new enemies registered");
        foreach (var name in new[]{"CrystalTurtle", "MoonMoth", "LanternFox"})
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_" + name + ".asset");
            Require(data != null && data.sprite != null, name + " sprite linked");
            var table = AssetDatabase.LoadAssetAtPath<EnemySpriteBounds>(EnemyEncounterBuilder.BoundsPath);
            Require(table.TryGet(data.sprite, out _), name + " grounded bounds");
        }
        var randomState = UnityEngine.Random.state;
        try
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                EnemyData previous = null;
                for (int i = 0; i < 100; i++)
                {
                    var enemy = db.GetRandomByTier(tier);
                    Require(enemy.tier == tier && enemy != previous, "tier and repetition");
                    previous = enemy;
                }
            }
        }
        finally { UnityEngine.Random.state = randomState; }
        Require(BattleActions.Fire.Resolve(10).Damage == 20 && BattleActions.Fire.MpCost == 2, "Fire preserved");
        foreach (var sound in new[]{SfxDirector.Attack,SfxDirector.Magic,SfxDirector.Hit,SfxDirector.Guard,"se_tap_fire","se_tap_ice","se_tap_thunder"})
            Require(Resources.Load<AudioClip>("Sound/" + sound) != null, sound + " loaded");
        const string iconPath = "Assets/Image/AppIcon/LilyIcon.jpg";
        var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
        importer.textureType = TextureImporterType.Default; importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[]{iconTexture});
        int iconCount = 0;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(UnityEditor.Build.NamedBuildTarget.iOS))
        {
            var icons = PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.iOS, kind);
            foreach (var icon in icons) icon.SetTexture(iconTexture);
            PlayerSettings.SetPlatformIcons(UnityEditor.Build.NamedBuildTarget.iOS, kind, icons);
            Require(icons.All(i => i.GetTexture() == iconTexture), "iOS icon slots assigned");
            iconCount += icons.Length;
        }
        Require(iconCount > 0, "iOS icons available");
        AssetDatabase.SaveAssets();
        Debug.Log("[CombatExpansion] Install and data validation PASS; enemies=" + db.enemies.Count + "; iOS icons=" + iconCount);
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
