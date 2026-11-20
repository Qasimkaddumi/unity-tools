using Kaddumi.UnityTools.Ads.Core;
using System;

namespace Kaddumi.UnityTools.Ads.Interfaces
{
    public interface IAdProvider
    {
        /// <summary>True when the user allowed personalised ads (passed to the SDK as consent).</summary>
        bool IsConsentGranted { get; }

        /// <summary>
        /// True when the provider may request ads at all. This is false only while consent is still
        /// undecided; a user who declined still gets ads, just non-personalised ones.
        /// </summary>
        bool CanRequestAds { get; }

        void Initialize(Action onComplete);

        void LoadAd(AdType adType, string adUnitId);

        /// <summary>
        /// Starts showing an ad. Returns false when nothing was shown (not ready, consent pending),
        /// in which case <see cref="OnAdClosed"/> will NOT be raised. When it returns true,
        /// <see cref="OnAdClosed"/> is raised exactly once, whether the ad closes or fails to show.
        /// </summary>
        bool ShowAd(AdType adType, string adUnitId);

        bool IsAdReady(AdType adType, string adUnitId);

        event Action<AdType> OnAdLoaded;
        event Action<AdType, AdErrorDomain> OnAdFailedToLoad;

        /// <summary>Raised when a shown ad is dismissed or fails to show.</summary>
        event Action<AdType> OnAdClosed;

        /// <param name="canRequestAds">The user has made a decision (or none is required).</param>
        /// <param name="personalized">The user allowed personalised ads.</param>
        void SetConsent(bool canRequestAds, bool personalized);

        event Action<AdReward> OnUserEarnedReward;
    }
}
