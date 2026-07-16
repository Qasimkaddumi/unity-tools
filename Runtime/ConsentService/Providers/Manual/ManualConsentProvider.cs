using System;
using Kaddumi.UnityTools.Consent.UI;

namespace Kaddumi.UnityTools.Consent.Providers
{
    /// <summary>
    /// Consent strategy for projects that do NOT use Google services. It gathers consent with
    /// the built-in <see cref="ConsentDialog"/>, persists the decision via <see cref="ConsentStorage"/>,
    /// and (optionally) only prompts users who are likely in a GDPR region.
    /// </summary>
    public class ManualConsentProvider : IConsentProvider
    {
        private readonly int _version;
        private readonly bool _restrictToGdprRegions;
        private readonly ConsentDialogSettings _dialogSettings;

        public ManualConsentProvider(int version, bool restrictToGdprRegions, ConsentDialogSettings dialogSettings)
        {
            _version = version;
            _restrictToGdprRegions = restrictToGdprRegions;
            _dialogSettings = dialogSettings ?? new ConsentDialogSettings();
        }

        public void RequestConsent(Action<ConsentStatus> onResolved)
        {
            // 1. A decision for the current version is already stored → reuse it, no prompt.
            if (ConsentStorage.HasStoredConsent(_version))
            {
                onResolved?.Invoke(ConsentStorage.Load());
                return;
            }

            // 2. Region gating: outside a GDPR region, consent isn't required — treat as granted.
            if (_restrictToGdprRegions && !ConsentRegion.IsLikelyGdprRegion())
            {
                ConsentStorage.Save(ConsentStatus.Granted, _version);
                onResolved?.Invoke(ConsentStatus.Granted);
                return;
            }

            // 3. Ask the user (resolves asynchronously when they tap a button).
            ShowDialog(onResolved);
        }

        public void ShowPrivacyOptions(Action<ConsentStatus> onResolved)
        {
            // Always re-present the dialog so the user can change their mind, regardless of region.
            ShowDialog(onResolved);
        }

        public void Reset()
        {
            ConsentStorage.Clear();
        }

        private void ShowDialog(Action<ConsentStatus> onResolved)
        {
            ConsentDialog.Present(_dialogSettings, accepted =>
            {
                var status = accepted ? ConsentStatus.Granted : ConsentStatus.Denied;
                ConsentStorage.Save(status, _version);
                onResolved?.Invoke(status);
            });
        }
    }
}
