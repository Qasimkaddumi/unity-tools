using System;
using System.Collections.Generic;
using UnityEngine;
using Kaddumi.UnityTools.Analytics.Interfaces;

#if GAMEANALYTICS_SDK_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Kaddumi.UnityTools.Analytics.Providers
{

    public class GameAnalyticsProvider : IAnalyticsProvider
    {
        private bool isReady = false;

        public void Initialize(Action onComplete)
        {
#if GAMEANALYTICS_SDK_INSTALLED
            GameAnalytics.Initialize();
            isReady = true;
            Debug.Log("[Analytics] GameAnalytics Initialized Successfully");
            onComplete?.Invoke();
#else
            Debug.LogWarning("[Analytics] GameAnalytics SDK not detected. Import the GameAnalytics package and define 'GAMEANALYTICS_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!isReady) return;

#if GAMEANALYTICS_SDK_INSTALLED
            if (parameters == null || parameters.Count == 0)
            {
                GameAnalytics.NewDesignEvent(eventName);
                return;
            }

            var fields = new Dictionary<string, object>(parameters);
            GameAnalytics.NewDesignEvent(eventName, fields);
#endif
        }

        public void SetUserProperty(string propertyName, string propertyValue)
        {
            if (!isReady) return;
#if GAMEANALYTICS_SDK_INSTALLED
            GameAnalytics.SetCustomDimension01(propertyValue);
#endif
        }

        public void SetUserId(string userId)
        {
            if (!isReady) return;
#if GAMEANALYTICS_SDK_INSTALLED
            GameAnalytics.SetCustomId(userId);
#endif
        }
    }
}
