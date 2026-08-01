using UnityEngine;
using UnityEngine.Timeline;

namespace Kaddumi.UnityTools.Audio.Timeline
{
    /// <summary>
    /// A Timeline track for <see cref="GameAudioClip"/> clips. Use this instead of
    /// Unity's built-in Audio Track so every sound goes through the shared audio
    /// system (SFX bus + mixer + settings) via <see cref="AudioManager"/>.
    /// Add it in the Timeline window: '+' -> Kaddumi.UnityTools.Audio -> Game Audio Track.
    /// </summary>
    [TrackColor(0.25f, 0.7f, 0.9f)]
    [TrackClipType(typeof(GameAudioClip))]
    public class GameAudioTrack : TrackAsset
    {
    }
}
