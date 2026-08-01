using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using UnityEngine;
using UnityEngine.Playables;

namespace Kaddumi.UnityTools.Audio.Timeline
{
    /// <summary>
    /// Runtime behaviour for a <see cref="GameAudioClip"/>. Starts the sound through the shared
    /// <see cref="AudioManager"/> (SFX bus + mixer) when the playhead enters the clip, and stops
    /// that voice when it leaves — so scrubbing/looping doesn't leave sounds hanging.
    /// </summary>
    public class GameAudioBehaviour : PlayableBehaviour
    {
        public SoundDefinition sound;

        private AudioHandle _handle = AudioHandle.Invalid;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            // Only fire during real playback, not editor scrubbing / preview.
            if (!Application.isPlaying) return;
            if (info.evaluationType != FrameData.EvaluationType.Playback) return;

            var manager = AudioManager.Instance;
            if (manager == null || sound == null) return;

            _handle = manager.PlaySfx2D(sound);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // Safe no-op on an Invalid or already-recycled handle.
            AudioManager.Instance?.Stop(_handle);
            _handle = AudioHandle.Invalid;
        }
    }
}
