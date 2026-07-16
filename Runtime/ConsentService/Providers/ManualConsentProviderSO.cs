using UnityEngine;
using Kaddumi.UnityTools.Consent.UI;

namespace Kaddumi.UnityTools.Consent.Providers
{
    /// <summary>
    /// Factory asset for the built-in manual consent flow (no Google services required).
    /// Create via <c>Assets ▸ Create ▸ Kaddumi ▸ Consent ▸ Providers ▸ Manual</c> and assign
    /// it on the ConsentService.
    /// </summary>
    [CreateAssetMenu(fileName = "ManualConsentProvider", menuName = "Kaddumi/Consent/Providers/Manual")]
    public class ManualConsentProviderSO : ConsentProviderSO
    {
        [Tooltip("Bump this whenever your privacy policy or vendor/purpose list changes. " +
                 "A higher number invalidates previously stored consent and re-prompts everyone.")]
        [SerializeField] private int consentVersion = 1;

        [Tooltip("When enabled, users detected outside the EEA/UK are not prompted and are " +
                 "treated as consented. When disabled, everyone is prompted.")]
        [SerializeField] private bool onlyAskInGdprRegions = true;

        [Tooltip("Text and privacy-policy link shown in the consent dialog.")]
        [SerializeField] private ConsentDialogSettings dialog = new ConsentDialogSettings();

        public override IConsentProvider CreateProvider()
            => new ManualConsentProvider(consentVersion, onlyAskInGdprRegions, dialog);
    }
}
