using Kaddumi.UnityTools.Analytics.Core;
using Kaddumi.UnityTools.Analytics.Providers;
using Kaddumi.UnityTools.Consent;
using Kaddumi.UnityTools.Services;
using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics
{

    public class AnalyticsManager : MonoBehaviour, IService
    {
        public static AnalyticsManager Instance { get; private set; }

        public AnalyticsService Service { get; private set; }


        [Header("Configuration")]
        [SerializeField] private bool enableDebugLogging = true;

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


            // 3. Register Providers
            if (enableDebugLogging && Application.isEditor)
            {
                Service.RegisterProvider(new DebugLoggerProvider(), onComplete);
            }
            else

            {
                Service.RegisterProvider(new FirebaseAnalyticsProvider(), onComplete);
            }

            // 4. Set Initial State
            Service.SetConsentStatus(ConsentService.Instance.IsConsentGranted);

            // 5. Check Flow
            if (ConsentService.Instance.IsConsentGranted)
            {
                // Already has permission from previous session
                Service.LogEvent("app_start");
            }

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