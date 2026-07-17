using Kaddumi.UnityTools.Audio.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Providers.UnityAudio
{
    /// <summary>
    /// Factory asset for the built-in Unity audio backend. This is the default provider;
    /// create one asset and assign it in the AudioManager inspector. Mirrors
    /// <c>PlayerPrefsSaveProviderSO</c>/<c>AdMobProviderSO</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "UnityAudioProvider", menuName = "Kaddumi/Audio/Providers/UnityAudio")]
    public class UnityAudioProviderSO : Providers.AudioProviderSO
    {
        public override IAudioProvider CreateProvider() => new UnityAudioProvider();
    }
}
