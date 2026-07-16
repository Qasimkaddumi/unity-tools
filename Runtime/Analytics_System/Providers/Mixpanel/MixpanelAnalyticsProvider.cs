using System;
using System.Collections.Generic;
using UnityEngine;
using Kaddumi.UnityTools.Analytics.Interfaces;

#if MIXPANEL_SDK_INSTALLED
using mixpanel;
#endif

namespace Kaddumi.UnityTools.Analytics.Providers
{

    public class MixpanelAnalyticsProvider : IAnalyticsProvider
    {
        private bool isReady = false;

        public void Initialize(Action onComplete)
        {
#if MIXPANEL_SDK_INSTALLED
            // The Mixpanel token is configured via Project Settings > Mixpanel.
            Mixpanel.Init();
            isReady = true;
            Debug.Log("[Analytics] Mixpanel Initialized Successfully");
            onComplete?.Invoke();
#else
            Debug.LogWarning("[Analytics] Mixpanel SDK not detected. Install 'com.mixpanel.unity' and define 'MIXPANEL_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!isReady) return;

#if MIXPANEL_SDK_INSTALLED
            if (parameters == null || parameters.Count == 0)
            {
                Mixpanel.Track(eventName);
                return;
            }

            var props = new Value();
            foreach (var kvp in parameters)
            {
                props[kvp.Key] = kvp.Value?.ToString();
            }

            Mixpanel.Track(eventName, props);
#endif
        }

        public void SetUserProperty(string propertyName, string propertyValue)
        {
            if (!isReady) return;
#if MIXPANEL_SDK_INSTALLED
            var props = new Value { [propertyName] = propertyValue };
            Mixpanel.People.Set(props);
#endif
        }

        public void SetUserId(string userId)
        {
            if (!isReady) return;
#if MIXPANEL_SDK_INSTALLED
            Mixpanel.Identify(userId);
#endif
        }
    }
}
