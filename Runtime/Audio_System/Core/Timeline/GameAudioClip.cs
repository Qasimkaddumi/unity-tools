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
    ///
    /// <para>The advertised <see cref="clipCaps"/> enable the same clip controls as Unity's
    /// built-in audio clip — ease in/out, blend curves, clip-in offset, speed multiplier, and
    /// looping — all of which are honoured at runtime by <see cref="GameAudioBehaviour"/>.</para>
    /// </summary>
    public class GameAudioClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("The sound asset to play for this clip. Routed through AudioManager -> SFX bus -> mixer.")]
        public SoundDefinition sound;

        [Tooltip("Per-clip volume multiplier, applied on top of the sound's own volume and the blend/ease weight.")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Loop the sound to fill the clip. The voice is always stopped when the playhead leaves the clip.")]
        public bool loop = false;

        // Ease in/out + blend curves (Blending), clip-in offset (ClipIn), speed multiplier
        // (SpeedMultiplier) and loop extrapolation (Looping) — matches Unity's audio clip caps.
        public ClipCaps clipCaps =>
            ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Looping;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<GameAudioBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.sound = sound;
            behaviour.volume = volume;
            behaviour.loop = loop;
            return playable;
        }
    }
}
