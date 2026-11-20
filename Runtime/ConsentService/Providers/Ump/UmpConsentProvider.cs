using System;
using UnityEngine;

#if AdMob_SDK_INSTALLED
using GoogleMobileAds.Ump.Api;
#endif

namespace Kaddumi.UnityTools.Consent.Providers
{
    /// <summary>
    /// Consent strategy backed by Google's User Messaging Platform (UMP). This shows Google's
    /// server-configured GDPR/CCPA form and derives the resolved status from the user's actual
    /// choices — not merely from the form closing:
    /// <list type="bullet">
    ///   <item><c>CanRequestAds()</c> false → <see cref="ConsentStatus.Unknown"/> (no ads yet).</item>
    ///   <item>GDPR doesn't apply, or the user consented to TCF purposes 1, 3 and 4 (store info,
    ///   build and use a personalised-ads profile) → <see cref="ConsentStatus.Granted"/>.</item>
    ///   <item>Otherwise (the user declined) → <see cref="ConsentStatus.Denied"/>: non-personalised ads only.</item>
    /// </list>
    /// <c>CanRequestAds()</c> alone is not enough: it is also true after the user rejects.
    ///
    /// Requires the Google Mobile Ads SDK (<c>AdMob_SDK_INSTALLED</c>). If the SDK is absent this
    /// provider logs a warning and treats consent as granted; use the Manual provider instead.
    /// </summary>
    public class UmpConsentProvider : IConsentProvider
    {
        private readonly int _version;
        private readonly bool _tagForUnderAgeOfConsent;

        public UmpConsentProvider(int version, bool tagForUnderAgeOfConsent)
        {
            _version = version;
            _tagForUnderAgeOfConsent = tagForUnderAgeOfConsent;
        }

        public void RequestConsent(Action<ConsentStatus> onResolved)
        {
#if AdMob_SDK_INSTALLED
            var request = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = _tagForUnderAgeOfConsent
            };

            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                {
                    Debug.LogError($"[UmpConsentProvider] Consent update failed: {updateError.Message}");
                    // Fall back to whatever UMP already knows (defaults to not-consented).
                    Resolve(onResolved);
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(showError =>
                {
                    if (showError != null)
                        Debug.LogError($"[UmpConsentProvider] Consent form failed: {showError.Message}");

                    Resolve(onResolved);
                });
            });
#else
            Debug.LogWarning("[UmpConsentProvider] Google Mobile Ads SDK not installed; UMP cannot run. " +
                             "Assign a Manual consent provider instead. Treating consent as Granted.");
            onResolved?.Invoke(ConsentStatus.Granted);
#endif
        }

        public ConsentStatus GetLastKnownStatus()
        {
#if AdMob_SDK_INSTALLED
            // UMP keeps the previous session's answer on the device, so this is safe to call
            // before ConsentInformation.Update (Google recommends doing exactly this at startup).
            return Evaluate();
#else
            return ConsentStatus.Granted;
#endif
        }

        public void ShowPrivacyOptions(Action<ConsentStatus> onResolved)
        {
#if AdMob_SDK_INSTALLED
            ConsentForm.ShowPrivacyOptionsForm(formError =>
            {
                if (formError != null)
                    Debug.LogError($"[UmpConsentProvider] Privacy options form failed: {formError.Message}");

                Resolve(onResolved);
            });
#else
            onResolved?.Invoke(ConsentStatus.Granted);
#endif
        }

        public void Reset()
        {
#if AdMob_SDK_INSTALLED
            ConsentInformation.Reset();
#endif
            ConsentStorage.Clear();
        }

#if AdMob_SDK_INSTALLED
        /// <summary>
        /// Maps UMP's resolved permission to our status and mirrors it to local storage so the
        /// last-known state is available synchronously at next startup.
        /// </summary>
        private void Resolve(Action<ConsentStatus> onResolved)
        {
            var status = Evaluate();

            ConsentStorage.Save(status, _version);
            onResolved?.Invoke(status);
        }

        private static ConsentStatus Evaluate()
        {
            if (!ConsentInformation.CanRequestAds())
                return ConsentStatus.Unknown;

            // IABTCF_gdprApplies: 1 = GDPR applies, 0 = it doesn't (missing is treated as 0).
            if (ReadTcfInt("IABTCF_gdprApplies") != 1)
                return ConsentStatus.Granted;

            // IABTCF_PurposeConsents is a '0'/'1' string; index 0 is purpose 1.
            string purposes = ReadTcfString("IABTCF_PurposeConsents");
            bool personalised = HasPurpose(purposes, 1) && HasPurpose(purposes, 3) && HasPurpose(purposes, 4);
            return personalised ? ConsentStatus.Granted : ConsentStatus.Denied;
        }

        private static bool HasPurpose(string purposes, int purpose)
        {
            int index = purpose - 1;
            return purposes != null && purposes.Length > index && purposes[index] == '1';
        }

        // UMP writes the IAB TCF values to the platform's default store: Android's default
        // SharedPreferences (which Unity's PlayerPrefs does NOT read) and iOS NSUserDefaults
        // (which PlayerPrefs does read).
        private static string ReadTcfString(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var prefs = GetAndroidDefaultPrefs())
                    return prefs.Call<string>("getString", key, "");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UmpConsentProvider] Could not read {key}: {e.Message}");
                return "";
            }
#else
            return PlayerPrefs.GetString(key, "");
#endif
        }

        private static int ReadTcfInt(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var prefs = GetAndroidDefaultPrefs())
                    return prefs.Call<int>("getInt", key, 0);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UmpConsentProvider] Could not read {key}: {e.Message}");
                return 0;
            }
#else
            return PlayerPrefs.GetInt(key, 0);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject GetAndroidDefaultPrefs()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var prefsManager = new AndroidJavaClass("android.preference.PreferenceManager"))
            {
                return prefsManager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity);
            }
        }
#endif
#endif
    }
}
