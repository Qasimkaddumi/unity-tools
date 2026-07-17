using System;
using Kaddumi.UnityTools.Audio.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Kaddumi.UnityTools.Audio.Data
{
    /// <summary>
    /// Shared configuration for the audio system. Assign one asset in the AudioManager
    /// inspector. Holds the <see cref="AudioMixer"/>, the exposed-parameter name for each
    /// bus, pool sizing, and default fade/duck timings. Mirrors <c>SaveConfig</c>/<c>AdConfig</c>.
    ///
    /// <para><b>Mixer setup:</b> create an AudioMixer with a group per <see cref="AudioBus"/>
    /// (Master/Music/SFX/UI/Ambience/Voice), right-click each group's Volume and "Expose to
    /// script", then paste the exposed names below. Volumes are stored/applied in decibels.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Kaddumi/Audio/AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [Header("Mixer")]
        [Tooltip("The AudioMixer whose groups the buses route through. Required for bus volume control and ducking.")]
        public AudioMixer Mixer;

        [Serializable]
        public class BusBinding
        {
            [Tooltip("Which logical bus this binding configures.")]
            public AudioBus Bus;

            [Tooltip("Exposed AudioMixer parameter controlling this bus's volume (in dB).")]
            public string ExposedVolumeParam;

            [Tooltip("AudioMixerGroup that voices on this bus are routed to.")]
            public AudioMixerGroup Group;

            [Tooltip("Default linear volume (0..1) used when there is no saved value yet.")]
            [Range(0f, 1f)] public float DefaultVolume = 1f;
        }

        [Tooltip("One entry per bus. Missing buses simply can't have their volume controlled.")]
        public BusBinding[] Buses = Array.Empty<BusBinding>();

        [Header("SFX Pool")]
        [Tooltip("Voices pre-created at startup for one-shot/looping SFX.")]
        [Min(1)] public int InitialPoolSize = 16;

        [Tooltip("Hard cap on pooled voices. The pool grows on demand up to this value, then steals the oldest voice.")]
        [Min(1)] public int MaxPoolSize = 48;

        [Header("Music")]
        [Tooltip("Default crossfade duration (seconds) when switching or stopping music, if the caller passes a negative value.")]
        [Min(0f)] public float DefaultMusicFadeSeconds = 1.5f;

        [Header("Ducking")]
        [Tooltip("Bus lowered by the DuckMusicForVoice helper (typically Music).")]
        public AudioBus DuckBus = AudioBus.Music;

        [Tooltip("Linear volume the ducked bus drops to during ducking.")]
        [Range(0f, 1f)] public float DuckTargetVolume = 0.25f;

        [Tooltip("Seconds to fade down into the duck.")]
        [Min(0f)] public float DuckAttackSeconds = 0.2f;

        [Tooltip("Seconds to hold at the ducked level before releasing (used by timed ducks).")]
        [Min(0f)] public float DuckHoldSeconds = 0f;

        [Tooltip("Seconds to fade back up out of the duck.")]
        [Min(0f)] public float DuckReleaseSeconds = 0.4f;

        [Header("Volume Range")]
        [Tooltip("Decibel value that represents silence (linear 0). Unity mixers bottom out around -80 dB.")]
        public float MinDecibels = -80f;

        /// <summary>
        /// Converts a linear 0..1 volume to decibels for an AudioMixer exposed parameter.
        /// Uses a log curve so the slider feels perceptually even; 0 maps to <see cref="MinDecibels"/>.
        /// </summary>
        public float LinearToDecibels(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            if (linear01 <= 0.0001f) return MinDecibels;
            return Mathf.Max(MinDecibels, Mathf.Log10(linear01) * 20f);
        }

        /// <summary>Inverse of <see cref="LinearToDecibels"/>; converts a mixer dB value back to linear 0..1.</summary>
        public float DecibelsToLinear(float decibels)
        {
            if (decibels <= MinDecibels) return 0f;
            return Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }

        /// <summary>Finds the binding for a bus, or null if it isn't configured.</summary>
        public BusBinding GetBinding(AudioBus bus)
        {
            if (Buses == null) return null;
            for (int i = 0; i < Buses.Length; i++)
            {
                if (Buses[i] != null && Buses[i].Bus == bus) return Buses[i];
            }
            return null;
        }
    }
}
