using Idle;

/// <summary>
/// 検証用の共通処理。ステージ制では起動すると拠点から始まり、敵はステージを選ぶまで出ない。
/// 「敵がいる本編」を前提にした検証（敵表示・クエスト入口・HUD・強化）は、これでステージ1を始めてから進める。
/// </summary>
public static class StageProbeSupport
{
    /// <summary>戦闘中になったら true。ステージ制でないシーンでは常に true</summary>
    public static bool EnsureBattle()
    {
        StageDirector director = StageDirector.Instance;
        if (director == null) return true;
        if (!director.IsReady) return false;
        if (director.CurrentPhase == StageDirector.Phase.Base) director.StartStage(1);
        return director.CurrentPhase == StageDirector.Phase.Battle;
    }
}
