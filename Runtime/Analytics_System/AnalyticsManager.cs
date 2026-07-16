using Kaddumi.UnityTools.Analytics.Core;
using Kaddumi.UnityTools.Analytics.Providers;
using Kaddumi.UnityTools.Consent;
using Kaddumi.UnityTools.Services;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics
{

    public class AnalyticsManager : MonoBehaviour, IService
    {
        public static AnalyticsManager Instance { get; private set; }

        public AnalyticsService Service { get; private set; }


        [Header("Configuration")]
        [Tooltip("Assign provider ScriptableObjects (e.g. Firebase, Debug Logger) to choose which analytics backends to use. All assigned providers receive events.")]
        [SerializeField] private List<AnalyticsProviderSO> providers = new List<AnalyticsProviderSO>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;


        }

        public void Initialize(Action onComplete)
        {
            InitializeAnalytics(onComplete);
        }


        private void InitializeAnalytics(Action onComplete)
        {
            // 1. Instantiate the Domain Services
            Service = new AnalyticsService();



            // 2. Wire up Consent to Analytics
            ConsentService.Instance.OnConsentChanged += (isGranted) =>
            {
                Service.SetConsentStatus(isGranted);

                if (isGranted)
                {
                    Debug.Log("[AnalyticsManager] Consent Granted. Tracking enabled.");
                }
                else
                {
                    Debug.Log("[AnalyticsManager] Consent Denied/Revoked. Tracking disabled.");
                }
            };


            // 3. Register the providers selected in the inspector
            var selectedProviders = new List<AnalyticsProviderSO>();
            foreach (var providerSO in providers)
            {
                if (providerSO != null) selectedProviders.Add(providerSO);
            }

            if (selectedProviders.Count == 0)
            {
                Debug.LogWarning("[AnalyticsManager] No analytics providers assigned in the inspector.");
                FinalizeInitialization(onComplete);
                return;
            }

            int remaining = selectedProviders.Count;
            foreach (var providerSO in selectedProviders)
            {
                Service.RegisterProvider(providerSO.CreateProvider(), () =>
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        FinalizeInitialization(onComplete);
                    }
                });
            }
        }

        // 4. Set initial consent state and fire the startup event once all providers are ready.
        private void FinalizeInitialization(Action onComplete)
        {
            Service.SetConsentStatus(ConsentService.Instance.IsConsentGranted);

            if (ConsentService.Instance.IsConsentGranted)
            {
                // Already has permission from previous session
                Service.LogEvent("app_start");
            }

            onComplete?.Invoke();
        }





        [ContextMenu("LogEvent")]
        public void TestEvent()
        {
            Service.LogEvent("TestEvent", new System.Collections.Generic.Dictionary<string, object>{
                { "Test Log with value", 10 },
                {"time", Time.time }
            });

        }
    }
}