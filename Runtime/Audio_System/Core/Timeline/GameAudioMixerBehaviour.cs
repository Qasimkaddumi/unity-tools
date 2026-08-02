using Kaddumi.UnityTools.Audio.Core;
using UnityEngine;
using UnityEngine.Playables;

namespace Kaddumi.UnityTools.Audio.Timeline
{
    /// <summary>
    /// Track mixer for <see cref="GameAudioTrack"/>. Runs every frame during Play-mode playback and
    /// drives one <see cref="AudioManager"/> voice per <see cref="GameAudioClip"/>: it starts a
    /// voice when a clip's input weight becomes non-zero and stops it when the weight returns to
    /// zero (or the graph tears down), applying the clip's blend weight to volume, its speed to
    /// pitch, its clip-in offset to the start position, and its loop toggle.
    ///
    /// <para>Reading input weight here is reliable, whereas the per-clip play/pause callbacks are
    /// not. Edit-mode auditioning is handled separately by the editor-only
    /// <c>GameAudioTimelinePreview</c>; this mixer intentionally does nothing outside Play mode.</para>
    /// </summary>
    public class GameAudioMixerBehaviour : PlayableBehaviour
    {
        private static bool _warnedNoManager;

        private AudioHandle[] _handles;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying) return;

            var manager = AudioManager.Instance;
            if (manager == null)
            {
                if (!_warnedNoManager)
                {
                    _warnedNoManager = true;
                    Debug.LogWarning("[GameAudio] A GameAudioTrack is playing but no AudioManager was " +
                                     "found in the scene. Add an AudioManager (registered under the " +
                                     "ServiceLocator and initialized) so Timeline audio can play.");
                }
                return;
            }

            int count = playable.GetInputCount();
            EnsureCapacity(count);

            for (int i = 0; i < count; i++)
            {
                var input = playable.GetInput(i);
                var script = (ScriptPlayable<GameAudioBehaviour>)input;
                var data = script.GetBehaviour();
                if (data == null || data.sound == null) continue;

                float weight = playable.GetInputWeight(i);
                bool active = weight > 0.0001f;
                bool playing = _handles[i].IsValid;

                if (active && !playing)
                {
                    _handles[i] = manager.PlaySfx2D(data.sound);
                    if (_handles[i].IsValid)
                    {
                        // Only override pitch for a non-default speed, so the sound's own
                        // PitchRange randomization is preserved for normal-speed clips.
                        float speed = (float)input.GetSpeed();
                        if (!Mathf.Approximately(speed, 1f)) manager.SetVoicePitch(_handles[i], speed);
                        manager.SetVoiceLoop(_handles[i], data.loop || data.sound.Loop);
                        manager.SetVoiceTime(_handles[i], (float)input.GetTime());
                    }
                }
                else if (!active && playing)
                {
                    manager.Stop(_handles[i]);
                    _handles[i] = AudioHandle.Invalid;
                }

                // Ease in/out + blend curves arrive as the input weight.
                if (_handles[i].IsValid)
                    manager.SetVoiceVolume(_handles[i], data.sound.Volume * data.volume * weight);
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info) => StopAll();

        public override void OnPlayableDestroy(Playable playable) => StopAll();

        private void EnsureCapacity(int count)
        {
            if (_handles != null && _handles.Length == count) return;

            var resized = new AudioHandle[count];
            for (int i = 0; i < count; i++)
                resized[i] = (_handles != null && i < _handles.Length) ? _handles[i] : AudioHandle.Invalid;
            _handles = resized;
        }

        private void StopAll()
        {
            if (_handles == null) return;
            var manager = AudioManager.Instance;
            for (int i = 0; i < _handles.Length; i++)
            {
                if (_handles[i].IsValid) manager?.Stop(_handles[i]);
                _handles[i] = AudioHandle.Invalid;
            }
        }
    }
}
