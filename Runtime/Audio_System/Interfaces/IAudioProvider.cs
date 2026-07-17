using System;
using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Interfaces
{
    /// <summary>
    /// Backend boundary for the audio system. Everything engine-specific — creating and
    /// pooling voices, crossfading music, routing buses to mixer groups, converting linear
    /// volume to decibels, and running duck/fade curves — lives behind this interface.
    ///
    /// <para>The default <c>UnityAudioProvider</c> drives Unity's built-in AudioSource/AudioMixer.
    /// A future FMOD/Wwise provider can implement the same contract with no changes to
    /// <c>AudioManager</c>. Mirrors <c>IAdProvider</c>/<c>ISaveProvider</c>.</para>
    /// </summary>
    public interface IAudioProvider
    {
        /// <summary>Boots the backend (pool, music sources, mixer bindings). Calls back when ready.</summary>
        void Initialize(AudioConfig config, Action onComplete);

        // --- Bus volume ------------------------------------------------------

        /// <summary>Sets a bus's volume from a linear 0..1 value (converted to dB internally).</summary>
        void SetBusVolume(AudioBus bus, float linear01);

        /// <summary>Mutes/unmutes a bus without losing its stored volume.</summary>
        void SetBusMuted(AudioBus bus, bool muted);

        // --- Playback --------------------------------------------------------

        /// <summary>Plays a one-shot or looping SFX. <paramref name="spatial"/> positions it in 3D at <paramref name="position"/>.</summary>
        AudioHandle PlaySfx(SoundDefinition def, Vector3 position, bool spatial);

        /// <summary>Crossfades to a new music track over <paramref name="fadeSeconds"/>.</summary>
        AudioHandle PlayMusic(SoundDefinition def, float fadeSeconds);

        /// <summary>Fades out and stops the current music.</summary>
        void StopMusic(float fadeSeconds);

        // --- Ducking ---------------------------------------------------------

        /// <summary>
        /// Ducks a bus down to <paramref name="toLinear01"/> over <paramref name="attackSeconds"/>,
        /// optionally holds for <paramref name="holdSeconds"/>, then restores over
        /// <paramref name="releaseSeconds"/>. A hold of 0 leaves the duck engaged until the next call.
        /// </summary>
        void Duck(AudioBus bus, float toLinear01, float attackSeconds, float releaseSeconds, float holdSeconds);

        // --- Voice control ---------------------------------------------------

        /// <summary>Stops a specific voice, fading over <paramref name="fadeSeconds"/>. No-op if the handle is stale.</summary>
        void Stop(AudioHandle handle, float fadeSeconds);

        /// <summary>Stops every active voice on a bus immediately.</summary>
        void StopBus(AudioBus bus);

        /// <summary>Pauses or resumes all voices (e.g. on a pause menu).</summary>
        void SetPaused(bool paused);
    }
}
