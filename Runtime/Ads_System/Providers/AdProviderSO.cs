using Kaddumi.UnityTools.Ads.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads.Providers
{
    /// <summary>
    /// Base ScriptableObject that acts as a factory for an <see cref="IAdProvider"/>.
    /// Create a concrete asset per provider and assign it in the AdManager inspector
    /// to switch providers without touching code.
    /// </summary>
    public abstract class AdProviderSO : ScriptableObject
    {
        /// <summary>Creates a fresh runtime provider instance.</summary>
        public abstract IAdProvider CreateProvider();
    }
}
