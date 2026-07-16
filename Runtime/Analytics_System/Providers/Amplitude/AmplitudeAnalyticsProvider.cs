using System;
using System.Collections.Generic;
using UnityEngine;
using Kaddumi.UnityTools.Analytics.Interfaces;

#if AMPLITUDE_SDK_INSTALLED
using AmplitudeSDK;
#endif

namespace Kaddumi.UnityTools.Analytics.Providers
{

    public class AmplitudeAnalyticsProvider : IAnalyticsProvider
    {
        private const string ApiKeyPlayerPrefKey = "Kaddumi.Analytics.Amplitude.ApiKey";

        private bool isReady = false;

#if AMPLITUDE_SDK_INSTALLED
        private Amplitude amplitude;
#endif

        public void Initialize(Action onComplete)
        {
#if AMPLITUDE_SDK_INSTALLED
            string apiKey = PlayerPrefs.GetString(ApiKeyPlayerPrefKey, string.Empty);
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError($"[Analytics] Amplitude API key missing. Store it under PlayerPrefs key '{ApiKeyPlayerPrefKey}' before initialization.");
                onComplete?.Invoke();
                return;
            }

            amplitude = Amplitude.getInstance();
            amplitude.init(apiKey);
            isReady = true;
            Debug.Log("[Analytics] Amplitude Initialized Successfully");
            onComplete?.Invoke();
#else
            Debug.LogWarning("[Analytics] Amplitude SDK not detected. Import the Amplitude Unity plugin and define 'AMPLITUDE_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!isReady) return;

#if AMPLITUDE_SDK_INSTALLED
            if (parameters == null || parameters.Count == 0)
            {
                amplitude.logEvent(eventName);
                return;
            }

            amplitude.logEvent(eventName, new Dictionary<string, object>(parameters));
#endif
        }

        public void SetUserProperty(string propertyName, string propertyValue)
        {
            if (!isReady) return;
#if AMPLITUDE_SDK_INSTALLED
            amplitude.setUserProperties(new Dictionary<string, object>
            {
                { propertyName, propertyValue }
            });
#endif
        }

        public void SetUserId(string userId)
        {
            if (!isReady) return;
#if AMPLITUDE_SDK_INSTALLED
            amplitude.setUserId(userId);
#endif
        }
    }
}
