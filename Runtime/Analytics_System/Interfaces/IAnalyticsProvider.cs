using System;
using System.Collections.Generic;

namespace Kaddumi.UnityTools.Analytics.Interfaces
{

    public interface IAnalyticsProvider
    {

        void Initialize(Action onComplete);

        void LogEvent(string eventName, Dictionary<string, object> parameters = null);


        void SetUserProperty(string propertyName, string propertyValue);

        void SetUserId(string userId);
    }
}