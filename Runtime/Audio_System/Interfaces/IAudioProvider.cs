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

        // --- Music stems (optional) ------------------------------------------
        // Default implementations keep existing third-party providers compiling; providers that
        // support layered music override them.

        /// <summary>
        /// Starts a layered music track: every stem in <paramref name="set"/> plays in sample-accurate
        /// sync, crossfading from whatever music was playing over <paramref name="fadeSeconds"/>.
        /// A negative fade means the config's <c>DefaultMusicFadeSeconds</c>. The default
        /// implementation logs a warning and does nothing.
        /// Calling it with the set that is already playing does nothing; call StopStems(0) first to restart it.
        /// </summary>
        void PlayStems(MusicStemSet set, float fadeSeconds)
        {
            Debug.LogWarning("[IAudioProvider] " + GetType().Name + " does not support music stems.");
        }

        /// <summary>
        /// Fades one stem of the currently playing stem set to <paramref name="linear01"/> over
        /// <paramref name="fadeSeconds"/> while all stems keep playing. A negative fade means the
        /// config's <c>DefaultMusicFadeSeconds</c>; 0 snaps. No-op by default.
        /// </summary>
        void SetStemVolume(string stemId, float linear01, float fadeSeconds) { }

        /// <summary>
        /// Fades out and stops the current stem set over <paramref name="fadeSeconds"/>
        /// (negative = config <c>DefaultMusicFadeSeconds</c>). No-op by default.
        /// </summary>
        void StopStems(float fadeSeconds) { }

        /// <summary>
        /// The ID of the stem set that is current (loading, playing, or fading in), or null when none
        /// is. Null by default.
        /// </summary>
        string CurrentStemSetId => null;

        /// <summary>
        /// Reads the current level (0..1) of one stem of the current set, before the set's volume and
        /// any crossfade are applied. Returns false and 0 when no set is current or the stem is
        /// unknown. Always false by default.
        /// </summary>
        bool TryGetStemVolume(string stemId, out float linear01)
        {
            linear01 = 0f;
            return false;
        }

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

        /// <summary>Sets a live voice's linear volume (per-source, on top of its bus). No-op if the handle is stale.</summary>
        void SetVoiceVolume(AudioHandle handle, float volume);

        /// <summary>
        /// Fades a live voice's linear volume to <paramref name="toVolume"/> over
        /// <paramref name="seconds"/> without stopping it. A duration of 0 or less snaps. No-op if the
        /// handle is stale. The default implementation snaps via <see cref="SetVoiceVolume"/>.
        /// </summary>
        void FadeVoice(AudioHandle handle, float toVolume, float seconds) => SetVoiceVolume(handle, toVolume);

        /// <summary>Sets a live voice's pitch / playback rate (1 = normal). No-op if the handle is stale.</summary>
        void SetVoicePitch(AudioHandle handle, float pitch);

        /// <summary>Toggles looping on a live voice. No-op if the handle is stale.</summary>
        void SetVoiceLoop(AudioHandle handle, bool loop);

        /// <summary>Seeks a live voice to a playback position in seconds (clamped to the clip). No-op if the handle is stale.</summary>
        void SetVoiceTime(AudioHandle handle, float seconds);

        /// <summary>Stops every active voice on a bus immediately.</summary>
        void StopBus(AudioBus bus);

        /// <summary>Pauses or resumes all voices (e.g. on a pause menu).</summary>
        void SetPaused(bool paused);
    }
}
