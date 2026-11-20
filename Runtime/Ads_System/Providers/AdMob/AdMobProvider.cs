using UnityEngine;
using System;
using Kaddumi.UnityTools.Ads.Core;
using Kaddumi.UnityTools.Ads.Interfaces;
using System.Collections.Generic;


#if AdMob_SDK_INSTALLED
using GoogleMobileAds.Api;
#endif


#if AppLovin_Adapter_INSTALLED
using GoogleMobileAds.Mediation.AppLovin.Api;
#endif

#if Liftoff_Adapter_INSTALLED
using GoogleMobileAds.Mediation.LiftoffMonetize.Api;
#endif
namespace Kaddumi.UnityTools.Ads.Providers
{

    public class AdMobProvider : IAdProvider
    {
        public bool IsConsentGranted { get; private set; }
        public bool CanRequestAds { get; private set; }
#if AdMob_SDK_INSTALLED
        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
        private BannerView bannerView;
#endif
        public event Action<AdType> OnAdLoaded;
        public event Action<AdType, AdErrorDomain> OnAdFailedToLoad;
        public event Action<AdType> OnAdClosed;
        public event Action<AdReward> OnUserEarnedReward;

        public void Initialize(Action onComplete)
        {
            Debug.Log("Initializing Ad Provider");

#if AdMob_SDK_INSTALLED

            List<string> testDeviceIds = new List<string>();
            testDeviceIds.Add("");

            RequestConfiguration requestConfiguration = new RequestConfiguration
            {
                TestDeviceIds = testDeviceIds
            };
            MobileAds.SetRequestConfiguration(requestConfiguration);

            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(initStatus =>
            {

                Dictionary<string, AdapterStatus> map = initStatus.getAdapterStatusMap();
                foreach (KeyValuePair<string, AdapterStatus> keyValuePair in map)
                {
                    string className = keyValuePair.Key;
                    AdapterStatus status = keyValuePair.Value;
                    switch (status.InitializationState)
                    {
                        case AdapterState.NotReady:
                            // The adapter initialization did not complete.
                            Debug.Log($"Adapter: {className} is not ready.");
                            break;
                        case AdapterState.Ready:
                            // The adapter was successfully initialized.
                            Debug.Log($"Adapter: {className} is initialized.");
                            break;
                    }
                }

                onComplete?.Invoke();
                Debug.Log("[AdMobProvider] Initialized.");
            });

#else
            onComplete?.Invoke();
            Debug.Log("[AdMobProvider] Initialized.");

#endif

            UpdateMediationConsent();
        }

        private void UpdateMediationConsent()
        {

            // Show consent dialog if needed and set consent status accordingly

#if AppLovin_Adapter_INSTALLED
            //------------- AppLovin -------------
            AppLovin.SetHasUserConsent(IsConsentGranted);
            //AppLovin.SetIsAgeRestrictedUser(true);

            //------------------------------------
#endif

#if UNITY_ADS_Adapter_INSTALLED
            // -------------  UnityAds -------------
            //UnityAds.SetConsentMetaData("gdpr.consent", IsConsentGranted);
            //UnityAds.SetConsentMetaData("privacy.consent", IsConsentGranted);

            System.Reflection.Assembly unityAdsAssembly = null;
            try
            {
                unityAdsAssembly = System.Reflection.Assembly.Load("GoogleMobileAds.Mediation.UnityAds.Api");
            }
            catch
            {
                Debug.LogWarning("[AdMobProvider] Unity Ads API assembly not found in this project. Skipping.");
            }

            if (unityAdsAssembly != null)
            {
                Type unityAdsType = unityAdsAssembly.GetType("GoogleMobileAds.Api.Mediation.UnityAds.UnityAds");
                if (unityAdsType != null)
                {
                    var method = unityAdsType.GetMethod("SetConsentMetaData", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        method.Invoke(null, new object[] { "gdpr.consent", IsConsentGranted });
                        method.Invoke(null, new object[] { "privacy.consent", IsConsentGranted });
                        Debug.Log("[AdMobProvider] Unity Ads consent applied via Reflection.");
                    }
                }
            }



#endif


#if Liftoff_Adapter_INSTALLED
            //------------- Liftoff -------------
            LiftoffMonetize.SetCCPAStatus(IsConsentGranted);

#endif


        }

        public void LoadAd(AdType adType, string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) return;
            if (!CanRequestAds)
            {
                Debug.Log($"[Ads] Load ad {adType} deferred: consent not decided yet");
                return;
            }


#if AdMob_SDK_INSTALLED
            // Clean up old ads before loading new ones
            switch (adType)
            {
                case AdType.Banner:
                    LoadBanner(adUnitId);
                    break;
                case AdType.Interstitial:
                    if (interstitialAd != null) interstitialAd.Destroy();
                    LoadInterstitial(adUnitId);
                    break;
                case AdType.Rewarded:
                    if (rewardedAd != null) rewardedAd.Destroy();
                    LoadRewarded(adUnitId);
                    break;
            }
#endif
        }

        public bool IsAdReady(AdType adType, string adUnitId)
        {
#if AdMob_SDK_INSTALLED
            switch (adType)
            {
                case AdType.Interstitial:
                    return interstitialAd != null && interstitialAd.CanShowAd();
                case AdType.Rewarded:
                    return rewardedAd != null && rewardedAd.CanShowAd();
                case AdType.Banner:
                    return bannerView != null;
                default:
                    return false;
            }
#else
            return false;
#endif
        }

        public bool ShowAd(AdType adType, string adUnitId)
        {
            if (!CanRequestAds)
            {
                Debug.Log($"[Ads] Show ad {adType} skipped: consent not decided yet");
                return false;
            }

#if AdMob_SDK_INSTALLED
            if (!IsAdReady(adType, adUnitId)) return false;

            switch (adType)
            {
                case AdType.Banner:
                    // Banner shows automatically on load usually, but can be managed here
                    bannerView.Show();
                    return true;
                case AdType.Interstitial:
                    interstitialAd.Show();
                    return true;
                case AdType.Rewarded:
                    rewardedAd.Show((Reward reward) =>
                    {
                        OnUserEarnedReward?.Invoke(new AdReward(reward.Type, reward.Amount));
                    });
                    return true;
            }
#endif
            return false;
        }

#if AdMob_SDK_INSTALLED
        /// <summary>
        /// Builds an ad request. Without personalisation consent the request is flagged
        /// non-personalised (npa=1), so users who declined still see ads.
        /// UMP's TCF string is also read by the SDK automatically.
        /// </summary>
        private AdRequest BuildRequest()
        {
            AdRequest request = new AdRequest();
            if (!IsConsentGranted)
            {
                request.Extras.Add("npa", "1");
            }
            return request;
        }
#endif

        // --- Implementation Details (AdMob Specifics) ---

        private void LoadBanner(string id)
        {
#if AdMob_SDK_INSTALLED
            if (bannerView != null) bannerView.Destroy();

            bannerView = new BannerView(id, AdSize.Banner, AdPosition.Bottom);

            // Create Request
            AdRequest request = BuildRequest();

            // Load
            bannerView.LoadAd(request);

#endif


            // There are no standard "Loaded" callbacks for Banner in the new API immediately useful for flow,
            // but you can hook into OnBannerAdLoaded if needed.
        }

        private void LoadInterstitial(string id)
        {
#if AdMob_SDK_INSTALLED
            AdRequest request = BuildRequest();

            InterstitialAd.Load(id, request, (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    OnAdFailedToLoad?.Invoke(AdType.Interstitial, new AdErrorDomain(error.GetCode(), error.GetMessage()));
                    return;
                }

                interstitialAd = ad;

                // Subscribe to AdMob specific events
                interstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    OnAdClosed?.Invoke(AdType.Interstitial);
                    interstitialAd.Destroy(); // Clean up
                    interstitialAd = null;
                };

                interstitialAd.OnAdFullScreenContentFailed += (AdError err) =>
                {
                    Debug.LogError($"[AdMobProvider] Interstitial failed to show: {err?.GetMessage()}");
                    interstitialAd.Destroy();
                    interstitialAd = null;
                    // Report it as closed so whoever is waiting on the ad can continue.
                    OnAdClosed?.Invoke(AdType.Interstitial);
                };

                OnAdLoaded?.Invoke(AdType.Interstitial);
            });
#endif
        }

        private void LoadRewarded(string id)
        {




#if AdMob_SDK_INSTALLED
            AdRequest request = BuildRequest();

            RewardedAd.Load(id, request, (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    OnAdFailedToLoad?.Invoke(AdType.Rewarded, new AdErrorDomain(error.GetCode(), error.GetMessage()));
                    return;
                }

                rewardedAd = ad;

                // Subscribe to AdMob specific events
                rewardedAd.OnAdFullScreenContentClosed += () =>
                {
                    OnAdClosed?.Invoke(AdType.Rewarded);
                    rewardedAd.Destroy();
                    rewardedAd = null;
                };

                rewardedAd.OnAdFullScreenContentFailed += (AdError err) =>
                {
                    Debug.LogError($"[AdMobProvider] Rewarded ad failed to show: {err?.GetMessage()}");
                    rewardedAd.Destroy();
                    rewardedAd = null;
                    // Report it as closed (no reward) so the caller's onFail runs.
                    OnAdClosed?.Invoke(AdType.Rewarded);
                };

                OnAdLoaded?.Invoke(AdType.Rewarded);
            });
#endif
        }

        public void SetConsent(bool canRequestAds, bool personalized)
        {
            CanRequestAds = canRequestAds;
            IsConsentGranted = personalized;
            UpdateMediationConsent();
        }
    }
}