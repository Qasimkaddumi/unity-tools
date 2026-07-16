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
    /// choices via <c>ConsentInformation.CanRequestAds()</c> — not merely from the form closing.
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
            var status = ConsentInformation.CanRequestAds()
                ? ConsentStatus.Granted
                : ConsentStatus.Denied;

            ConsentStorage.Save(status, _version);
            onResolved?.Invoke(status);
        }
#endif
    }
}
