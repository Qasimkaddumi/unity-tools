using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    /// <summary>
    /// Base ScriptableObject that acts as a factory for an <see cref="ISaveProvider"/>.
    /// Create a concrete asset per backend (PlayerPrefs, file, encrypted file) and assign
    /// it in the SaveManager inspector to switch storage without touching code.
    /// Mirrors <c>AuthProviderSO</c>.
    /// </summary>
    public abstract class SaveProviderSO : ScriptableObject
    {
        /// <summary>Creates a fresh runtime provider instance from this asset's settings.</summary>
        public abstract ISaveProvider CreateProvider();
    }
}
