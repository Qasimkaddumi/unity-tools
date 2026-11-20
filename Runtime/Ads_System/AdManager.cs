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

        [Tooltip("Assign a provider ScriptableObject (e.g. AdMobProvider) to choose which ad SDK to use.")]
        [SerializeField] private AdProviderSO adProvider;

        // Polling interval for connectivity checks (Design Doc Section 10)
        [SerializeField] private bool autoLoadAds = true;

        private IAdProvider provider;

        // Temporary storage for the reward callback
        private Action currentRewardOnSuccess;
        private Action currentRewardOnFail;
        private Action currentInterstitiaLCallback;
        private bool userEarnedRewardThisSession = false;
        private bool providerInitialized;

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

            if (adProvider == null)
            {
                Debug.LogError("[AdManager] No Ad Provider assigned. Assign an AdProviderSO in the inspector.");
                onComplete?.Invoke();
                return;
            }

            provider = adProvider.CreateProvider();
            ApplyConsent();
            if (ConsentService.Instance != null)
            {
                ConsentService.Instance.OnConsentChanged += HandleConsentChanged;
            }


            // Subscribe to events
            provider.OnAdLoaded += HandleAdLoaded;
            provider.OnAdFailedToLoad += HandleAdFailedToLoad;
            provider.OnAdClosed += HandleAdClosed;
            provider.OnUserEarnedReward += HandleUserEarnedReward;

            // Initialize the SDK
            provider.Initialize(() =>
            {
                providerInitialized = true;
                Debug.LogWarning($"{nameof(AdManager)} Initialized");
                onComplete?.Invoke();
                PreloadFullscreenAds();
            });
        }

        private void OnDestroy()
        {
            if (ConsentService.Instance != null)
            {
                ConsentService.Instance.OnConsentChanged -= HandleConsentChanged;
            }
        }

        private void ApplyConsent()
        {
            var consent = ConsentService.Instance;
            bool canRequestAds = consent != null && consent.CanRequestAds;
            bool personalized = consent != null && consent.IsConsentGranted;
            provider.SetConsent(canRequestAds, personalized);
        }

        private void HandleConsentChanged(bool personalized)
        {
            ApplyConsent();
            Debug.Log($"[AdManager] Consent changed. Can request ads: {provider.CanRequestAds}, personalised: {personalized}");

            // Ads skipped at startup because consent was still pending: load them now.
            PreloadFullscreenAds();
        }

        private void PreloadFullscreenAds()
        {
            if (!providerInitialized || !autoLoadAds || !provider.CanRequestAds) return;

            if (!provider.IsAdReady(AdType.Interstitial, adConfig.GetAdUnitId(AdType.Interstitial)))
                PreLoadAd(AdType.Interstitial);
            if (!provider.IsAdReady(AdType.Rewarded, adConfig.GetAdUnitId(AdType.Rewarded)))
                PreLoadAd(AdType.Rewarded);
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
            if (provider == null)
            {
                Debug.LogWarning("[AdManager] ShowInterstitial called before initialization.");
                onClosed?.Invoke();
                return;
            }

            string id = adConfig.GetAdUnitId(AdType.Interstitial);

            // Store the callback first in case the SDK reports the close synchronously.
            currentInterstitiaLCallback = onClosed;
            if (provider.IsAdReady(AdType.Interstitial, id) && provider.ShowAd(AdType.Interstitial, id))
            {
                return;
            }

            Debug.LogWarning("[AdManager] Interstitial not shown (not ready or consent pending).");
            currentInterstitiaLCallback = null;
            // Try to load one for next time
            provider.LoadAd(AdType.Interstitial, id);
            onClosed?.Invoke();
        }


        public void ShowRewardedAd(Action onSuccess, Action onFail)
        {
            if (provider == null)
            {
                Debug.LogWarning("[AdManager] ShowRewardedAd called before initialization.");
                onFail?.Invoke();
                return;
            }

            userEarnedRewardThisSession = false;
            string id = adConfig.GetAdUnitId(AdType.Rewarded);

            // Store the callbacks first in case the SDK reports the close synchronously.
            currentRewardOnSuccess = onSuccess;
            currentRewardOnFail = onFail;
            if (provider.IsAdReady(AdType.Rewarded, id) && provider.ShowAd(AdType.Rewarded, id))
            {
                return;
            }

            Debug.LogWarning("[AdManager] Rewarded ad not shown (not ready or consent pending).");
            currentRewardOnSuccess = null;
            currentRewardOnFail = null;
            // Attempt reload
            provider.LoadAd(AdType.Rewarded, id);
            onFail?.Invoke();
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