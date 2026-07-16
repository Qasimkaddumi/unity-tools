using Kaddumi.UnityTools.Ads.Core;
using Kaddumi.UnityTools.Ads.Data;
using Kaddumi.UnityTools.Ads.Interfaces;
using Kaddumi.UnityTools.Ads.Providers;
using Kaddumi.UnityTools.Consent;
using Kaddumi.UnityTools.Services;
using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads
{

    public class AdManager : MonoBehaviour, IService
    {
        public static AdManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private AdConfig adConfig;

        // Polling interval for connectivity checks (Design Doc Section 10)
        [SerializeField] private bool autoLoadAds = true;

        private IAdProvider provider;

        // Temporary storage for the reward callback
        private Action currentRewardOnSuccess;
        private Action currentRewardOnFail;
        private Action currentInterstitiaLCallback;
        private bool userEarnedRewardThisSession = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;


            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(Action onComplete)
        {
            Debug.Log("Initializing Ad manager");

            provider = new AdMobProvider();
            provider.SetConsentStatus(Consent.ConsentService.Instance.IsConsentGranted);
            ConsentService.Instance.OnConsentChanged += (consent) =>
            {
                provider.SetConsentStatus(Consent.ConsentService.Instance.IsConsentGranted);
                Debug.LogWarning($"User consent status: {Consent.ConsentService.Instance.IsConsentGranted}");
            };


            // Subscribe to events
            provider.OnAdLoaded += HandleAdLoaded;
            provider.OnAdFailedToLoad += HandleAdFailedToLoad;
            provider.OnAdClosed += HandleAdClosed;
            provider.OnUserEarnedReward += HandleUserEarnedReward;

            // Initialize the SDK
            provider.Initialize(() =>
            {
                Debug.LogWarning($"{nameof(AdManager)} Initialized");
                onComplete?.Invoke();
                PreLoadAd(AdType.Interstitial);
                PreLoadAd(AdType.Rewarded);
            });



        }




        public void ShowBanner()
        {
            string id = adConfig.GetAdUnitId(AdType.Banner);

            provider.LoadAd(AdType.Banner, id);

            // provider.ShowAd(AdType.Banner, id);
            Debug.LogWarning("showw banner");

        }

        public void ShowInterstitial(Action onClosed = null)
        {
            string id = adConfig.GetAdUnitId(AdType.Interstitial);
            // provider.LoadAd(AdType.Interstitial, id);

            if (provider.IsAdReady(AdType.Interstitial, id))
            {
                currentInterstitiaLCallback = onClosed;
                provider.ShowAd(AdType.Interstitial, id);
            }
            else
            {
                Debug.LogWarning("[AdManager] Interstitial not ready.");
                // Optional: Try to load one for next time
                provider.LoadAd(AdType.Interstitial, id);
                onClosed?.Invoke();
            }
        }


        public void ShowRewardedAd(Action onSuccess, Action onFail)
        {
            userEarnedRewardThisSession = false;
            string id = adConfig.GetAdUnitId(AdType.Rewarded);

            if (provider.IsAdReady(AdType.Rewarded, id))
            {
                currentRewardOnSuccess = onSuccess;
                currentRewardOnFail = onFail;
                provider.ShowAd(AdType.Rewarded, id);
            }
            else
            {
                Debug.LogWarning("[AdManager] Rewarded Ad not ready.");
                // Attempt reload
                onFail?.Invoke();
                provider.LoadAd(AdType.Rewarded, id);

            }
        }


        private void HandleAdLoaded(AdType type)
        {
            Debug.Log($"[AdManager] {type} Loaded.");
        }

        private void HandleAdFailedToLoad(AdType type, AdErrorDomain error)
        {
            Debug.LogError($"[AdManager] Failed to load {type}: {error.Message}");
            // Section 10: Logic for exponential backoff could go here
        }

        private void HandleAdClosed(AdType type)
        {
            Debug.Log($"[AdManager] {type} Closed.");

            if (type == AdType.Interstitial)
            {
                currentInterstitiaLCallback?.Invoke();
                currentInterstitiaLCallback = null;
            }
            else if (type == AdType.Rewarded)
            {
                if (userEarnedRewardThisSession)
                {
                    Debug.Log("[AdManager] Rewarded Ad closed after earning reward. Firing OnSuccess.");
                    currentRewardOnSuccess?.Invoke();
                }
                else if (currentRewardOnFail != null)
                {
                    Debug.Log("[AdManager] Rewarded Ad closed early/without reward. Firing OnFail.");
                    currentRewardOnFail?.Invoke();
                }

                currentRewardOnSuccess = null;
                currentRewardOnFail = null;
                userEarnedRewardThisSession = false;
            }

            if (autoLoadAds)
            {
                PreLoadAd(type);
            }
        }


        private void PreLoadAd(AdType type)
        {
            Debug.Log($"[AdManager] Preloading {type}.");
            string id = adConfig.GetAdUnitId(type);
            provider.LoadAd(type, id);
        }

        private void HandleUserEarnedReward(AdReward reward)
        {
            Debug.Log($"[AdManager] Reward Earned: {reward.Amount} {reward.Type}");
            userEarnedRewardThisSession = true;
        }


    }
}