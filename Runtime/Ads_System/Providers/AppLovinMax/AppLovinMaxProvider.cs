using UnityEngine;
using System;
using Kaddumi.UnityTools.Ads.Core;
using Kaddumi.UnityTools.Ads.Interfaces;

// AppLovin MAX provider (standalone MAX SDK, NOT the AdMob mediation adapter).
// Requires the AppLovin MAX Unity plugin and the AppLovinMAX_SDK_INSTALLED scripting
// define symbol (Player Settings > Scripting Define Symbols).
// The MAX SDK API (MaxSdk / MaxSdkCallbacks) lives in the global namespace, so no using directive is needed.

namespace Kaddumi.UnityTools.Ads.Providers
{
    /// <summary>
    /// <see cref="IAdProvider"/> backed by AppLovin MAX mediation.
    /// MAX identifies each ad slot solely by its Ad Unit ID, so no ad instances are cached here.
    /// </summary>
    public class AppLovinMaxProvider : IAdProvider
    {
        // Assign your AppLovin SDK Key here (from the AppLovin dashboard).
        private const string SdkKey = "YOUR_APPLOVIN_SDK_KEY";

        public bool IsConsentGranted { get; private set; }

        // MAX callbacks report only the ad unit id, so remember the banner id to Show/Hide it.
        private string bannerAdUnitId;
        private bool eventsHooked;

        public event Action<AdType> OnAdLoaded;
        public event Action<AdType, AdErrorDomain> OnAdFailedToLoad;
        public event Action<AdType> OnAdClosed;
        public event Action<AdReward> OnUserEarnedReward;

        public void Initialize(Action onComplete)
        {
            Debug.Log("[AppLovinMaxProvider] Initializing.");

#if AppLovinMAX_SDK_INSTALLED
            // Consent must be applied before initialization so it reaches mediated networks.
            ApplyConsent();

            HookEvents();

            MaxSdkCallbacks.OnSdkInitializedEvent += _ =>
            {
                Debug.Log("[AppLovinMaxProvider] Initialized.");
                onComplete?.Invoke();
            };

            MaxSdk.SetSdkKey(SdkKey);
            MaxSdk.InitializeSdk();
#else
            onComplete?.Invoke();
            Debug.Log("[AppLovinMaxProvider] Initialized (SDK not installed - no-op).");
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

#if AppLovinMAX_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Banner:
                    if (bannerAdUnitId != adUnitId)
                    {
                        MaxSdk.CreateBanner(adUnitId, MaxSdkBase.BannerPosition.BottomCenter);
                        bannerAdUnitId = adUnitId;
                    }
                    break;
                case AdType.Interstitial:
                    MaxSdk.LoadInterstitial(adUnitId);
                    break;
                case AdType.Rewarded:
                    MaxSdk.LoadRewardedAd(adUnitId);
                    break;
            }
#endif
        }

        public bool IsAdReady(AdType adType, string adUnitId)
        {
#if AppLovinMAX_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Interstitial:
                    return MaxSdk.IsInterstitialReady(adUnitId);
                case AdType.Rewarded:
                    return MaxSdk.IsRewardedAdReady(adUnitId);
                case AdType.Banner:
                    return bannerAdUnitId == adUnitId;
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

#if AppLovinMAX_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Banner:
                    MaxSdk.ShowBanner(adUnitId);
                    break;
                case AdType.Interstitial:
                    if (IsAdReady(adType, adUnitId)) MaxSdk.ShowInterstitial(adUnitId);
                    break;
                case AdType.Rewarded:
                    if (IsAdReady(adType, adUnitId)) MaxSdk.ShowRewardedAd(adUnitId);
                    break;
            }
#endif
        }

#if AppLovinMAX_SDK_INSTALLED
        private void HookEvents()
        {
            if (eventsHooked) return;
            eventsHooked = true;

            // Interstitial
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (_, __) => OnAdLoaded?.Invoke(AdType.Interstitial);
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (_, error) =>
                OnAdFailedToLoad?.Invoke(AdType.Interstitial, new AdErrorDomain((int)error.Code, error.Message));
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (_, __) => OnAdClosed?.Invoke(AdType.Interstitial);
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (adUnitId, error, ___) =>
                OnAdClosed?.Invoke(AdType.Interstitial);

            // Rewarded
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (_, __) => OnAdLoaded?.Invoke(AdType.Rewarded);
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (_, error) =>
                OnAdFailedToLoad?.Invoke(AdType.Rewarded, new AdErrorDomain((int)error.Code, error.Message));
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (_, __) => OnAdClosed?.Invoke(AdType.Rewarded);
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (adUnitId, error, ___) =>
                OnAdClosed?.Invoke(AdType.Rewarded);
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (_, reward, __) =>
                OnUserEarnedReward?.Invoke(new AdReward(reward.Label, reward.Amount));

            // Banner
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += (_, __) => OnAdLoaded?.Invoke(AdType.Banner);
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += (_, error) =>
                OnAdFailedToLoad?.Invoke(AdType.Banner, new AdErrorDomain((int)error.Code, error.Message));
        }

        private void ApplyConsent()
        {
            MaxSdk.SetHasUserConsent(IsConsentGranted);
        }
#endif

        public void SetConsentStatus(bool granted)
        {
            IsConsentGranted = granted;
#if AppLovinMAX_SDK_INSTALLED
            ApplyConsent();
#endif
        }
    }
}
