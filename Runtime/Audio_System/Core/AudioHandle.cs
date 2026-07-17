namespace Kaddumi.UnityTools.Audio.Core
{
    /// <summary>
    /// Lightweight, value-type reference to a playing (or recently played) voice.
    /// Returned by the play APIs so callers can later stop or fade a specific sound.
    ///
    /// <para>Because SFX voices are pooled and recycled, a raw voice index isn't safe to
    /// hold onto — by the time you use it, that voice may already be playing a different
    /// sound. Each handle therefore carries a <see cref="Generation"/> stamp that the
    /// provider bumps every time the voice is reused. A handle whose generation no longer
    /// matches the voice is treated as expired, so stopping a stale handle is a safe no-op.</para>
    /// </summary>
    public readonly struct AudioHandle
    {
        /// <summary>Index of the underlying voice in the provider's pool. -1 for an invalid handle.</summary>
        public readonly int VoiceIndex;

        /// <summary>Reuse counter captured when this handle was issued. Used to detect recycled voices.</summary>
        public readonly int Generation;

        public AudioHandle(int voiceIndex, int generation)
        {
            VoiceIndex = voiceIndex;
            Generation = generation;
        }

        /// <summary>A handle that never refers to a live voice (e.g. returned when a play call fails).</summary>
        public static AudioHandle Invalid => new AudioHandle(-1, 0);

        /// <summary>True when this handle points at a real voice slot. Does not guarantee the voice is still playing.</summary>
        public bool IsValid => VoiceIndex >= 0;
    }
}
