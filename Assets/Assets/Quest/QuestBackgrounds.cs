using UnityEngine;

/// <summary>
/// クエスト用背景画像の置き場所と名前。画像は Assets/Resources/Backgrounds/ に置く（Resources.Load で読む）。
/// Prefab は QuestProductionUIBuilder で作り直されるため、画像の割り当てはコード側で解決する。
/// 無い画像は null を返し、呼び出し側は現行の森（既定の背景）のままにする。
/// </summary>
public static class QuestBackgrounds
{
    public const string PathForest = "path_forest"; // 森の小道（左カード・道中戦闘）
    public const string PathRoad   = "path_road";   // 古い街道（右カード・道中戦闘）
    public const string Village    = "village";     // 村人イベント
    public const string Ruins      = "ruins";       // 宝箱イベント
    public const string Boss       = "boss";        // ボス戦

    /// <summary>
    /// 暗めの背景か。既存の紺色の文字（明るい森向け）は、暗い背景ではクリーム色に切り替える。
    /// 明るい「森の小道」と既定の森は、従来の紺色のまま読める。
    /// </summary>
    public static bool IsDark(string name)
    {
        return name == Village || name == Ruins || name == PathRoad || name == Boss;
    }

    public static Sprite Get(string name)
    {
        return Resources.Load<Sprite>("Backgrounds/" + name);
    }
}
