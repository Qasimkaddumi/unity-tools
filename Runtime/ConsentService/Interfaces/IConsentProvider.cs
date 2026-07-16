using System;

namespace Kaddumi.UnityTools.Consent
{
    /// <summary>
    /// A pluggable strategy for gathering and re-presenting privacy consent.
    ///
    /// Implementations are created by a <see cref="Providers.ConsentProviderSO"/> factory and
    /// driven by <see cref="ConsentService"/>. The two shipped implementations are:
    /// <list type="bullet">
    ///   <item>Google User Messaging Platform (server-backed GDPR/CCPA form).</item>
    ///   <item>A built-in manual dialog for projects that don't use Google services.</item>
    /// </list>
    /// </summary>
    public interface IConsentProvider
    {
        /// <summary>
        /// Resolve the user's consent state, showing UI if a decision is required.
        ///
        /// <paramref name="onResolved"/> is invoked with the final status. It may be called
        /// synchronously (when a decision is already stored or not required in the user's
        /// region) or asynchronously (after the user answers a form/dialog). If UI must be
        /// shown, the method returns without calling back until the user responds.
        /// </summary>
        void RequestConsent(Action<ConsentStatus> onResolved);

        /// <summary>
        /// Re-open the consent UI so the user can review or change a previous choice,
        /// regardless of region. Invokes <paramref name="onResolved"/> with the new status.
        /// </summary>
        void ShowPrivacyOptions(Action<ConsentStatus> onResolved);

        /// <summary>Clear any consent state this provider persists.</summary>
        void Reset();
    }
}
