using Kaddumi.UnityTools.Analytics.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{

    public class DebugLoggerProvider : IAnalyticsProvider
    {
        public void Initialize(Action onComplete)
        {
            Debug.Log("[Analytics] Debug Logger Provider Initialized");
            onComplete?.Invoke();
        }

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            string paramString = parameters != null ?
                string.Join(", ", parameters) : "None";

            Debug.Log($"<color=cyan>[Analytics-Debug]</color> Event: {eventName} | Params: {paramString}");
        }

        public void SetUserProperty(string propertyName, string propertyValue)
        {
            Debug.Log($"<color=cyan>[Analytics-Debug]</color> User Property: {propertyName} = {propertyValue}");
        }

        public void SetUserId(string userId)
        {
            Debug.Log($"<color=cyan>[Analytics-Debug]</color> User ID set to: {userId}");
        }
    }
}