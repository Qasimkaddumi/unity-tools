using UnityEngine;

namespace Kaddumi.UnityTools.Consent.Providers
{
    /// <summary>
    /// Factory asset for the Google UMP consent flow (requires the Google Mobile Ads SDK).
    /// Create via <c>Assets ▸ Create ▸ Kaddumi ▸ Consent ▸ Providers ▸ Google UMP</c> and assign
    /// it on the ConsentService.
    /// </summary>
    [CreateAssetMenu(fileName = "UmpConsentProvider", menuName = "Kaddumi/Consent/Providers/Google UMP")]
    public class UmpConsentProviderSO : ConsentProviderSO
    {
        [Tooltip("Bump this when your policy changes so the locally mirrored status is refreshed. " +
                 "Google UMP manages the authoritative form state on its side.")]
        [SerializeField] private int consentVersion = 1;

        [Tooltip("Set true if your app is directed at children / users under the age of consent.")]
        [SerializeField] private bool tagForUnderAgeOfConsent = false;

        public override IConsentProvider CreateProvider()
            => new UmpConsentProvider(consentVersion, tagForUnderAgeOfConsent);
    }
}
