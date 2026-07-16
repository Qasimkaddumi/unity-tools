using UnityEngine;
using System;
using Kaddumi.UnityTools.Ads.Core;
using Kaddumi.UnityTools.Ads.Interfaces;

// Unity LevelPlay (ironSource) provider.
// Requires the "com.unity.services.levelplay" package (SDK 8.x, Init API) and the
// LevelPlay_SDK_INSTALLED scripting define symbol (Player Settings > Scripting Define Symbols).
#if LevelPlay_SDK_INSTALLED
using Unity.Services.LevelPlay;
#endif

namespace Kaddumi.UnityTools.Ads.Providers
{
    /// <summary>
    /// <see cref="IAdProvider"/> backed by Unity LevelPlay (ironSource) mediation.
    /// Uses the SDK 8.x instance-based ad objects and the static <c>LevelPlay.Init</c> API.
    /// </summary>
    public class LevelPlayProvider : IAdProvider
    {
        // Assign your LevelPlay App Key here (from the LevelPlay dashboard).
        // Kept as a field so it can be surfaced later via config if desired.
        private const string AppKey = "YOUR_LEVELPLAY_APP_KEY";

        public bool IsConsentGranted { get; private set; }

#if LevelPlay_SDK_INSTALLED
        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd rewardedAd;
        private LevelPlayBannerAd bannerAd;
#endif

        public event Action<AdType> OnAdLoaded;
        public event Action<AdType, AdErrorDomain> OnAdFailedToLoad;
        public event Action<AdType> OnAdClosed;
        public event Action<AdReward> OnUserEarnedReward;

        public void Initialize(Action onComplete)
        {
            Debug.Log("[LevelPlayProvider] Initializing.");

#if LevelPlay_SDK_INSTALLED
            // Consent must be set before Init so it propagates to all mediated networks.
            ApplyConsent();

            LevelPlay.OnInitSuccess += config =>
            {
                Debug.Log("[LevelPlayProvider] Initialized.");
                onComplete?.Invoke();
            };
            LevelPlay.OnInitFailed += error =>
            {
                Debug.LogError($"[LevelPlayProvider] Init failed: {error.ErrorMessage}");
                onComplete?.Invoke();
            };

            LevelPlay.Init(AppKey);
#else
            onComplete?.Invoke();
            Debug.Log("[LevelPlayProvider] Initialized (SDK not installed - no-op).");
#endif
        }

        public void LoadAd(AdType adType, string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) return;
            if (!IsConsentGranted)
            {
                Debug.Log($"[Ads] Load ad {adType} was blocked due to lack of consent");
                return;
            }

#if LevelPlay_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Banner:
                    LoadBanner(adUnitId);
                    break;
                case AdType.Interstitial:
                    LoadInterstitial(adUnitId);
                    break;
                case AdType.Rewarded:
                    LoadRewarded(adUnitId);
                    break;
            }
#endif
        }

        public bool IsAdReady(AdType adType, string adUnitId)
        {
#if LevelPlay_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Interstitial:
                    return interstitialAd != null && interstitialAd.IsAdReady();
                case AdType.Rewarded:
                    return rewardedAd != null && rewardedAd.IsAdReady();
                case AdType.Banner:
                    return bannerAd != null;
                default:
                    return false;
            }
#else
            return false;
#endif
        }

        public void ShowAd(AdType adType, string adUnitId)
        {
            if (!IsConsentGranted)
            {
                Debug.Log($"[Ads] Show ad {adType} was blocked due to lack of consent");
                return;
            }

#if LevelPlay_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Banner:
                    if (bannerAd != null) bannerAd.ShowAd();
                    break;
                case AdType.Interstitial:
                    if (IsAdReady(adType, adUnitId)) interstitialAd.ShowAd();
                    break;
                case AdType.Rewarded:
                    if (IsAdReady(adType, adUnitId)) rewardedAd.ShowAd();
                    break;
            }
#endif
        }

        // --- LevelPlay specifics ---

#if LevelPlay_SDK_INSTALLED
        private void LoadInterstitial(string id)
        {
            if (interstitialAd == null)
            {
                interstitialAd = new LevelPlayInterstitialAd(id);
                interstitialAd.OnAdLoaded += _ => OnAdLoaded?.Invoke(AdType.Interstitial);
                interstitialAd.OnAdLoadFailed += error =>
                    OnAdFailedToLoad?.Invoke(AdType.Interstitial, new AdErrorDomain(error.ErrorCode, error.ErrorMessage));
                interstitialAd.OnAdClosed += _ => OnAdClosed?.Invoke(AdType.Interstitial);
            }
            interstitialAd.LoadAd();
        }

        private void LoadRewarded(string id)
        {
            if (rewardedAd == null)
            {
                rewardedAd = new LevelPlayRewardedAd(id);
                rewardedAd.OnAdLoaded += _ => OnAdLoaded?.Invoke(AdType.Rewarded);
                rewardedAd.OnAdLoadFailed += error =>
                    OnAdFailedToLoad?.Invoke(AdType.Rewarded, new AdErrorDomain(error.ErrorCode, error.ErrorMessage));
                rewardedAd.OnAdClosed += _ => OnAdClosed?.Invoke(AdType.Rewarded);
                rewardedAd.OnAdRewarded += (_, reward) =>
                    OnUserEarnedReward?.Invoke(new AdReward(reward.Name, reward.Amount));
            }
            rewardedAd.LoadAd();
        }

        private void LoadBanner(string id)
        {
            if (bannerAd != null) bannerAd.DestroyAd();

            bannerAd = new LevelPlayBannerAd(id);
            bannerAd.OnAdLoaded += _ => OnAdLoaded?.Invoke(AdType.Banner);
            bannerAd.OnAdLoadFailed += error =>
                OnAdFailedToLoad?.Invoke(AdType.Banner, new AdErrorDomain(error.ErrorCode, error.ErrorMessage));
            bannerAd.LoadAd();
        }

        private void ApplyConsent()
        {
            // Propagated to every mediated network that supports a consent API.
            IronSource.Agent.setConsent(IsConsentGranted);
        }
#endif

        public void SetConsentStatus(bool granted)
        {
            IsConsentGranted = granted;
#if LevelPlay_SDK_INSTALLED
            ApplyConsent();
#endif
        }
    }
}
