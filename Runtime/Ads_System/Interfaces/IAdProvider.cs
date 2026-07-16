using Kaddumi.UnityTools.Ads.Core;
using System;

namespace Kaddumi.UnityTools.Ads.Interfaces
{
    public interface IAdProvider
    {
        bool IsConsentGranted { get; }
        void Initialize(Action onComplete);

        void LoadAd(AdType adType, string adUnitId);

        void ShowAd(AdType adType, string adUnitId);

        bool IsAdReady(AdType adType, string adUnitId);



        event Action<AdType> OnAdLoaded;
        event Action<AdType, AdErrorDomain> OnAdFailedToLoad;


        event Action<AdType> OnAdClosed;

        void SetConsentStatus(bool granted);
        event Action<AdReward> OnUserEarnedReward;
    }
}
