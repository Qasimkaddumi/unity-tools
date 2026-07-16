using UnityEngine;
using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Ads.Core;
using Kaddumi.UnityTools.Ads.Interfaces;

// Unity Ads used directly (the com.unity.ads / "Advertisement" SDK), NOT via AdMob mediation.
// Requires the Advertisement package and the UnityAds_SDK_INSTALLED scripting define symbol
// (Player Settings > Scripting Define Symbols).
#if UnityAds_SDK_INSTALLED
using UnityEngine.Advertisements;
#endif

namespace Kaddumi.UnityTools.Ads.Providers
{
    /// <summary>
    /// <see cref="IAdProvider"/> backed by the Unity Ads (Advertisement) SDK.
    /// The SDK is callback/listener based, so this class also implements the Unity Ads
    /// listener interfaces (only when the SDK define is present).
    /// </summary>
    public class UnityAdsProvider : IAdProvider
#if UnityAds_SDK_INSTALLED
        , IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
#endif
    {
        // Game IDs from the Unity dashboard (distinct from per-placement ad unit ids).
        private const string AndroidGameId = "YOUR_ANDROID_GAME_ID";
        private const string IOSGameId = "YOUR_IOS_GAME_ID";
        private const bool TestMode = false;

        public bool IsConsentGranted { get; private set; }

        // Unity Ads reports only the placement id in callbacks, so map each placement to its AdType.
        private readonly Dictionary<string, AdType> placementTypes = new Dictionary<string, AdType>();
        private readonly HashSet<string> loadedPlacements = new HashSet<string>();

        public event Action<AdType> OnAdLoaded;
        public event Action<AdType, AdErrorDomain> OnAdFailedToLoad;
        public event Action<AdType> OnAdClosed;
        public event Action<AdReward> OnUserEarnedReward;

        public void Initialize(Action onComplete)
        {
            Debug.Log("[UnityAdsProvider] Initializing.");
            pendingInit = onComplete;

#if UnityAds_SDK_INSTALLED
            ApplyConsent();

            string gameId = Application.platform == RuntimePlatform.Android ? AndroidGameId : IOSGameId;

            if (Advertisement.isInitialized)
            {
                CompleteInit();
                return;
            }
            Advertisement.Initialize(gameId, TestMode, this);
#else
            CompleteInit();
            Debug.Log("[UnityAdsProvider] Initialized (SDK not installed - no-op).");
#endif
        }

        private Action pendingInit;

        private void CompleteInit()
        {
            var cb = pendingInit;
            pendingInit = null;
            cb?.Invoke();
        }

        public void LoadAd(AdType adType, string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) return;
            if (!IsConsentGranted)
            {
                Debug.Log($"[Ads] Load ad {adType} was blocked due to lack of consent");
                return;
            }

            placementTypes[adUnitId] = adType;

#if UnityAds_SDK_INSTALLED
            if (adType == AdType.Banner)
            {
                Advertisement.Banner.Load(adUnitId, new BannerLoadOptions
                {
                    loadCallback = () =>
                    {
                        loadedPlacements.Add(adUnitId);
                        OnAdLoaded?.Invoke(AdType.Banner);
                    },
                    errorCallback = message =>
                        OnAdFailedToLoad?.Invoke(AdType.Banner, new AdErrorDomain(0, message))
                });
                return;
            }

            loadedPlacements.Remove(adUnitId);
            Advertisement.Load(adUnitId, this);
#endif
        }

        public bool IsAdReady(AdType adType, string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) return false;
            return loadedPlacements.Contains(adUnitId);
        }

        public void ShowAd(AdType adType, string adUnitId)
        {
            if (!IsConsentGranted)
            {
                Debug.Log($"[Ads] Show ad {adType} was blocked due to lack of consent");
                return;
            }

#if UnityAds_SDK_INSTALLED
            if (adType == AdType.Banner)
            {
                Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
                Advertisement.Banner.Show(adUnitId);
                return;
            }

            if (IsAdReady(adType, adUnitId))
            {
                Advertisement.Show(adUnitId, this);
            }
#endif
        }

        private void ApplyConsent()
        {
#if UnityAds_SDK_INSTALLED
            string value = IsConsentGranted ? "true" : "false";

            MetaData gdprMetaData = new MetaData("gdpr");
            gdprMetaData.Set("consent", value);
            Advertisement.SetMetaData(gdprMetaData);

            MetaData privacyMetaData = new MetaData("privacy");
            privacyMetaData.Set("consent", value);
            Advertisement.SetMetaData(privacyMetaData);
#endif
        }

        public void SetConsentStatus(bool granted)
        {
            IsConsentGranted = granted;
            ApplyConsent();
        }

#if UnityAds_SDK_INSTALLED
        private AdType TypeFor(string placementId) =>
            placementTypes.TryGetValue(placementId, out AdType type) ? type : AdType.Interstitial;

        // --- IUnityAdsInitializationListener ---
        public void OnInitializationComplete()
        {
            Debug.Log("[UnityAdsProvider] Initialized.");
            CompleteInit();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogError($"[UnityAdsProvider] Init failed: {error} - {message}");
            CompleteInit();
        }

        // --- IUnityAdsLoadListener ---
        public void OnUnityAdsAdLoaded(string placementId)
        {
            loadedPlacements.Add(placementId);
            OnAdLoaded?.Invoke(TypeFor(placementId));
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            OnAdFailedToLoad?.Invoke(TypeFor(placementId), new AdErrorDomain((int)error, message));
        }

        // --- IUnityAdsShowListener ---
        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            loadedPlacements.Remove(placementId);
            OnAdClosed?.Invoke(TypeFor(placementId));
        }

        public void OnUnityAdsShowStart(string placementId) { }

        public void OnUnityAdsShowClick(string placementId) { }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
        {
            loadedPlacements.Remove(placementId);

            AdType type = TypeFor(placementId);
            if (type == AdType.Rewarded && showCompletionState == UnityAdsShowCompletionState.COMPLETED)
            {
                // Unity Ads has no reward type/amount concept; surface a single completed reward.
                OnUserEarnedReward?.Invoke(new AdReward("reward", 1));
            }

            OnAdClosed?.Invoke(type);
        }
#endif
    }
}
