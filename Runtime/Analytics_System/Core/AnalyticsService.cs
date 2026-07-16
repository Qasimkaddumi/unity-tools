using Kaddumi.UnityTools.Analytics.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Core
{

    public class AnalyticsService
    {
        private readonly List<IAnalyticsProvider> providers;
        private bool isConsentGranted;

        public AnalyticsService()
        {
            providers = new List<IAnalyticsProvider>();
            isConsentGranted = false;
        }

        public void RegisterProvider(IAnalyticsProvider provider, Action onComplete)
        {
            if (provider == null) return;

            provider.Initialize(onComplete);
            providers.Add(provider);
        }

        public void SetConsentStatus(bool granted)
        {
            isConsentGranted = granted;
        }

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!isConsentGranted)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[Analytics] Event '{eventName}' blocked due to lack of consent.");
#endif
                return;
            }

            foreach (var provider in providers)
            {
                try
                {
                    provider.LogEvent(eventName, parameters);
                }
                catch (Exception ex)
                {
                    // Catching errors here prevents analytics failures from breaking game logic
                    Debug.LogError($"[Analytics] Error in provider {provider.GetType().Name}: {ex.Message}");
                }
            }
        }

        public void SetUserProperty(string name, string value)
        {
            if (!isConsentGranted) return;

            foreach (var provider in providers)
            {
                provider.SetUserProperty(name, value);
            }
        }

        public void SetUserId(string userId)
        {
            foreach (var provider in providers)
            {
                provider.SetUserId(userId);
            }
        }
    }
}