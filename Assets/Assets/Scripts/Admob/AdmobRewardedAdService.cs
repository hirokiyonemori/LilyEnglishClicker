using System;
using LilyEnglish.Growth;

namespace Idle
{
    /// <summary>
    /// 強化ショップの「広告を見て EXP」を、AdMob のリワード広告につなぐ。
    /// 都度 AdmobManager.instance を見るので、シーンが入れ替わって AdmobManager が作り直されても追従する。
    /// </summary>
    public sealed class AdmobRewardedAdService : IRewardedAdService
    {
        public bool IsReady
        {
            get { return AdmobManager.instance != null && AdmobManager.instance.IsRewardedReady; }
        }

        public void Show(Action<bool> onComplete)
        {
            AdmobManager manager = AdmobManager.instance;
            if (manager == null || !manager.IsRewardedReady)
            {
                if (onComplete != null) onComplete(false);
                return;
            }
            manager.ShowRewarded(onComplete);
        }
    }
}
