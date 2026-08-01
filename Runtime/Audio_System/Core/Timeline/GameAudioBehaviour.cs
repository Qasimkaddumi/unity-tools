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
    ///
    /// <para>Honours the Timeline clip settings: the per-frame blend <c>weight</c> (from ease
    /// in/out durations and blend curves) drives the voice volume, the clip's speed multiplier
    /// drives pitch, the clip-in offset seeks the start position, and the loop toggle fills the
    /// clip length.</para>
    /// </summary>
    public class GameAudioBehaviour : PlayableBehaviour
    {
        // Set by GameAudioClip.CreatePlayable.
        public SoundDefinition sound;
        public float volume = 1f;
        public bool loop;

        private AudioHandle _handle = AudioHandle.Invalid;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            // Only fire during real playback, not editor scrubbing / preview.
            if (!Application.isPlaying) return;
            if (info.evaluationType != FrameData.EvaluationType.Playback) return;

            var manager = AudioManager.Instance;
            if (manager == null || sound == null) return;

            _handle = manager.PlaySfx2D(sound);
            if (!_handle.IsValid) return;

            // Speed multiplier -> pitch; loop toggle (or the sound's own) -> fill the clip;
            // clip-in offset (and any mid-clip start) -> seek. Then set the initial volume from
            // the current blend weight so an ease-in doesn't pop to full volume for a frame.
            // Only override pitch when a non-default speed is set, so the sound's own PitchRange
            // randomization is preserved for normal-speed clips.
            float speed = (float)playable.GetSpeed();
            if (!Mathf.Approximately(speed, 1f)) manager.SetVoicePitch(_handle, speed);
            manager.SetVoiceLoop(_handle, loop || sound.Loop);
            manager.SetVoiceTime(_handle, (float)playable.GetTime());
            manager.SetVoiceVolume(_handle, sound.Volume * volume * info.weight);
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!_handle.IsValid) return;
            var manager = AudioManager.Instance;
            if (manager == null) return;

            // Ease in/out + blend curves arrive as the playable's per-frame weight.
            float baseVolume = sound != null ? sound.Volume : 1f;
            manager.SetVoiceVolume(_handle, baseVolume * volume * info.weight);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // Safe no-op on an Invalid or already-recycled handle.
            AudioManager.Instance?.Stop(_handle);
            _handle = AudioHandle.Invalid;
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            // Safety net: make sure a looping voice never outlives its graph.
            AudioManager.Instance?.Stop(_handle);
            _handle = AudioHandle.Invalid;
        }
    }
}
