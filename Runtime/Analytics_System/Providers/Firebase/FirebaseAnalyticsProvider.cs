using System;
using System.Collections.Generic;
using UnityEngine;
using Kaddumi.UnityTools.Analytics.Interfaces;



#if FIREBASE_SDK_INSTALLED
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif

namespace Kaddumi.UnityTools.Analytics.Providers
{

    public class FirebaseAnalyticsProvider : IAnalyticsProvider
    {
        private bool isFirebaseReady = false;

        public void Initialize(Action onComplete)
        {
#if FIREBASE_SDK_INSTALLED
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    isFirebaseReady = true;
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    Debug.Log("[Analytics] Firebase Initialized Successfully");
                }
                else
                {
                    Debug.LogError($"[Analytics] Could not resolve all Firebase dependencies: {dependencyStatus}");
                }
                onComplete?.Invoke();
            });
#else
            Debug.LogWarning("[Analytics] Firebase SDK not detected. Define 'FIREBASE_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (!isFirebaseReady) return;

#if FIREBASE_SDK_INSTALLED
            if (parameters == null || parameters.Count == 0)
            {
                FirebaseAnalytics.LogEvent(eventName);
                return;
            }

            // Convert Dictionary to Firebase Parameters
            Parameter[] firebaseParams = new Parameter[parameters.Count];
            int index = 0;

            foreach (var kvp in parameters)
            {
                if (kvp.Value is long lVal)
                    firebaseParams[index] = new Parameter(kvp.Key, lVal);
                else if (kvp.Value is int iVal)
                    firebaseParams[index] = new Parameter(kvp.Key, iVal);
                else if (kvp.Value is double dVal)
                    firebaseParams[index] = new Parameter(kvp.Key, dVal);
                else if (kvp.Value is float fVal)
                    firebaseParams[index] = new Parameter(kvp.Key, (double)fVal);
                else
                    firebaseParams[index] = new Parameter(kvp.Key, kvp.Value.ToString());

                index++;
            }

            FirebaseAnalytics.LogEvent(eventName, firebaseParams);
#endif
        }

        public void SetUserProperty(string propertyName, string propertyValue)
        {
            if (!isFirebaseReady) return;
#if FIREBASE_SDK_INSTALLED
            FirebaseAnalytics.SetUserProperty(propertyName, propertyValue);
#endif
        }

        public void SetUserId(string userId)
        {
            if (!isFirebaseReady) return;
#if FIREBASE_SDK_INSTALLED
            FirebaseAnalytics.SetUserId(userId);
#endif
        }
    }
}