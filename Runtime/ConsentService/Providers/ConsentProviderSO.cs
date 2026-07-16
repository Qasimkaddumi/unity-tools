using UnityEngine;

namespace Kaddumi.UnityTools.Consent.Providers
{
    /// <summary>
    /// Base ScriptableObject that acts as a factory for an <see cref="IConsentProvider"/>.
    /// Create a concrete asset (UMP or Manual) and assign it in the ConsentService inspector
    /// to switch consent strategies without touching code.
    /// </summary>
    public abstract class ConsentProviderSO : ScriptableObject
    {
        /// <summary>Creates a fresh runtime consent provider instance.</summary>
        public abstract IConsentProvider CreateProvider();
    }
}
