using System;
using System.Collections.Generic;
using UnityEngine;
using Kaddumi.UnityTools.Analytics.Interfaces;

#if UNITY_ANALYTICS_SDK_INSTALLED
using Unity.Services.Core;
using Unity.Services.Analytics;
#endif

namespace Kaddumi.UnityTools.Analytics.Providers
{

    public class UnityAnalyticsProvider : IAnalyticsProvider
    {
        private bool isReady = false;

        public void Initialize(Action onComplete)
        {
#if UNITY_ANALYTICS_SDK_INSTALLED
            InitializeAsync(onComplete);
#else
            Debug.LogWarning("[Analytics] Unity Analytics SDK not detected. Install 'com.unity.services.analytics' and define 'UNITY_ANALYTICS_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

#if UNITY_ANALYTICS_SDK_INSTALLED
        private async void InitializeAsync(Action onComplete)
        {
            try
            {
                await UnityServices.InitializeAsync();
                AnalyticsService.Instance.StartDataCollection();
                isReady = true;
                Debug.Log("[Analytics] Unity Analytics Initialized Successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Analytics] Failed to initialize Unity Analytics: {ex.Message}");
            }
            finally
            {
                onComplete?.Invoke();
            }
        }
#endif

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!isReady) return;

#if UNITY_ANALYTICS_SDK_INSTALLED
            var customEvent = new CustomEvent(eventName);
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    customEvent[kvp.Key] = kvp.Value;
                }
            }

            AnalyticsService.Instance.RecordEvent(customEvent);
#endif
        }

        public void SetUserProperty(string propertyName, string propertyValue)
        {
            if (!isReady) return;
#if UNITY_ANALYTICS_SDK_INSTALLED
            // Unity Analytics has no direct user-property API; forward as a single-parameter event.
            LogEvent("set_user_property", new Dictionary<string, object>
            {
                { "property_name", propertyName },
                { "property_value", propertyValue }
            });
#endif
        }

        public void SetUserId(string userId)
        {
            if (!isReady) return;
#if UNITY_ANALYTICS_SDK_INSTALLED
            AnalyticsService.Instance.SetUserId(userId);
#endif
        }
    }
}
