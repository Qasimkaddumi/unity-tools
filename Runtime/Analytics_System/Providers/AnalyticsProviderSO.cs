using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    /// <summary>
    /// Base ScriptableObject that acts as a factory for an <see cref="IAnalyticsProvider"/>.
    /// Create a concrete asset per provider and assign it in the AnalyticsManager inspector
    /// to switch providers without touching code.
    /// </summary>
    public abstract class AnalyticsProviderSO : ScriptableObject
    {
        /// <summary>Creates a fresh runtime provider instance.</summary>
        public abstract IAnalyticsProvider CreateProvider();
    }
}
