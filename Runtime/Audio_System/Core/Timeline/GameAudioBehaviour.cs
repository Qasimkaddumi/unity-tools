using Kaddumi.UnityTools.Audio.Data;
using UnityEngine.Playables;

namespace Kaddumi.UnityTools.Audio.Timeline
{
    /// <summary>
    /// Per-clip data for a <see cref="GameAudioClip"/>, carried on the clip's playable and read by
    /// <see cref="GameAudioMixerBehaviour"/> on the track. Playback is driven by the track mixer
    /// (whose per-frame callback is reliably invoked), not by this behaviour's play/pause
    /// callbacks, which don't fire dependably for audio timing.
    /// </summary>
    public class GameAudioBehaviour : PlayableBehaviour
    {
        public SoundDefinition sound;
        public float volume = 1f;
        public bool loop;
    }
}
