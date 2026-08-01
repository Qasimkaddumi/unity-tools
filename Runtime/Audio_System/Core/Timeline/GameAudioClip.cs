using Kaddumi.UnityTools.Audio.Data;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Kaddumi.UnityTools.Audio.Timeline
{
    /// <summary>
    /// A Timeline clip that plays a <see cref="SoundDefinition"/> through the shared
    /// <see cref="AudioManager"/> (SFX bus + mixer), instead of Timeline's built-in Audio
    /// Track which bypasses the audio system. Drag a SoundDefinition asset onto a clip on a
    /// <see cref="GameAudioTrack"/>.
    /// </summary>
    public class GameAudioClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("The sound asset to play at this clip's start. Routed through AudioManager -> SFX bus -> mixer.")]
        public SoundDefinition sound;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<GameAudioBehaviour>.Create(graph);
            playable.GetBehaviour().sound = sound;
            return playable;
        }
    }
}
