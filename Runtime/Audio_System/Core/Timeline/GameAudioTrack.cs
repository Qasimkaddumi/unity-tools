using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Kaddumi.UnityTools.Audio.Timeline
{
    /// <summary>
    /// A Timeline track for <see cref="GameAudioClip"/> clips. Use this instead of
    /// Unity's built-in Audio Track so every sound goes through the shared audio
    /// system (SFX bus + mixer + settings) via <see cref="AudioManager"/>.
    /// Add it in the Timeline window: '+' -> Kaddumi.UnityTools.Audio -> Game Audio Track.
    ///
    /// <para>Playback is driven by a track mixer (<see cref="GameAudioMixerBehaviour"/>) whose
    /// per-frame callback reliably starts/stops each clip's voice by input weight — unlike the
    /// individual clip play/pause callbacks, which don't fire dependably for audio timing.</para>
    /// </summary>
    [TrackColor(0.25f, 0.7f, 0.9f)]
    [TrackClipType(typeof(GameAudioClip))]
    public class GameAudioTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<GameAudioMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
