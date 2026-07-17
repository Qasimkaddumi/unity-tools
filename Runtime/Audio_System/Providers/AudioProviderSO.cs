using Kaddumi.UnityTools.Audio.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Providers
{
    /// <summary>
    /// Base ScriptableObject that acts as a factory for an <see cref="IAudioProvider"/>.
    /// Create a concrete asset per backend (Unity built-in today; FMOD/Wwise later) and
    /// assign it in the AudioManager inspector to switch backends without touching code.
    /// Mirrors <c>SaveProviderSO</c>/<c>AdProviderSO</c>.
    /// </summary>
    public abstract class AudioProviderSO : ScriptableObject
    {
        /// <summary>Creates a fresh runtime provider instance from this asset's settings.</summary>
        public abstract IAudioProvider CreateProvider();
    }
}
