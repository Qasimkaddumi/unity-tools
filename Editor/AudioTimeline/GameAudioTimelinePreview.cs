using System;
using System.Reflection;
using Kaddumi.UnityTools.Audio.Timeline;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Kaddumi.UnityTools.Audio.Timeline.Editor
{
    /// <summary>
    /// Auditions <see cref="GameAudioClip"/> clips in the Timeline window during edit-mode
    /// playback, mirroring Unity's built-in audio track. Runs from an editor update loop rather
    /// than the clip's <see cref="GameAudioBehaviour"/> callbacks, because:
    /// <list type="bullet">
    /// <item>it must play <b>only while the Play button is pressed</b>, not while scrubbing — so
    /// it gates on <c>PlayableGraph.IsPlaying()</c> (false during scrub), and</item>
    /// <item>it must stay <b>in sync</b> with the playhead — so it seeks the preview to the media
    /// position derived from the director's current time instead of starting from the clip's
    /// beginning with callback latency.</item>
    /// </list>
    /// A runtime <c>AudioSource</c> is silent in edit mode (no AudioListener), so playback goes
    /// through Unity's internal editor preview player (<c>UnityEditor.AudioUtil</c>, via
    /// reflection). One clip is auditioned at a time; Play mode handles full multi-voice mixing.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameAudioTimelinePreview
    {
        private static readonly MethodInfo PlayMethod;   // PlayPreviewClip(AudioClip, int, bool)
        private static readonly MethodInfo StopAllMethod; // StopAllPreviewClips()
        private static readonly MethodInfo SetPosMethod; // SetPreviewClipSamplePosition(AudioClip, int)
        private static readonly MethodInfo GetPosMethod; // GetPreviewClipSamplePosition()

        private static AudioClip _currentAudio;
        private static TimelineClip _currentClip;

        static GameAudioTimelinePreview()
        {
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil != null)
            {
                PlayMethod = Find(audioUtil, new[] { "PlayPreviewClip", "PlayClip" },
                    new[] { typeof(AudioClip), typeof(int), typeof(bool) });
                StopAllMethod = Find(audioUtil, new[] { "StopAllPreviewClips", "StopAllClips" },
                    Type.EmptyTypes);
                SetPosMethod = Find(audioUtil, new[] { "SetPreviewClipSamplePosition", "SetClipSamplePosition" },
                    new[] { typeof(AudioClip), typeof(int) });
                GetPosMethod = Find(audioUtil, new[] { "GetPreviewClipSamplePosition", "GetClipSamplePosition" },
                    Type.EmptyTypes);
            }

            if (PlayMethod == null)
            {
                Debug.LogWarning("[GameAudio] Editor audio preview is unavailable: could not find " +
                                 "UnityEditor.AudioUtil.PlayPreviewClip. Timeline clips will still " +
                                 "play in Play mode, but won't audition in the Timeline window.");
            }

            EditorApplication.update += Update;
            StopAll(); // silence anything orphaned across a domain reload
        }

        private static void Update()
        {
            // Play mode drives audio through the runtime AudioManager; don't double up here.
            if (EditorApplication.isPlayingOrWillChangePlaymode) { StopIfPreviewing(); return; }

            PlayableDirector director = TimelineEditor.inspectedDirector;
            if (director == null)
            {
                StopIfPreviewing();
                return;
            }

            // IsPlaying() is true only when the Timeline Play button is engaged — not while
            // scrubbing or paused. This is what keeps auditioning to actual playback.
            PlayableGraph graph = director.playableGraph;
            if (!graph.IsValid() || !graph.IsPlaying())
            {
                StopIfPreviewing();
                return;
            }

            double time = director.time;
            TimelineClip clip = FindActiveClip(director, time);
            AudioClip audio = clip != null ? GetAudioClip(clip) : null;
            if (audio == null)
            {
                StopIfPreviewing();
                return;
            }

            double mediaTime = clip.clipIn + (time - clip.start) * clip.timeScale;
            int sample = Mathf.Clamp((int)(mediaTime * audio.frequency), 0, Mathf.Max(0, audio.samples - 1));
            bool loop = ShouldLoop(clip);

            if (audio != _currentAudio || clip != _currentClip)
            {
                // Newly-active clip: start it already seeked to the synced position.
                StopAll();
                PlayPreview(audio, sample, loop);
                _currentAudio = audio;
                _currentClip = clip;
            }
            else if (SetPosMethod != null && GetPosMethod != null)
            {
                // Same clip: nudge back into sync only after a real drift (e.g. an editor hitch),
                // so we don't stutter by re-seeking every frame.
                object pos = GetPosMethod.Invoke(null, null);
                if (pos is int actual && Mathf.Abs(actual - sample) > audio.frequency / 10) // >100 ms
                    SetPosMethod.Invoke(null, new object[] { audio, sample });
            }
        }

        private static TimelineClip FindActiveClip(PlayableDirector director, double time)
        {
            if (!(director.playableAsset is TimelineAsset timeline)) return null;

            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (!(track is GameAudioTrack) || track.muted) continue;
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (time >= clip.start && time < clip.end) return clip;
                }
            }
            return null;
        }

        private static AudioClip GetAudioClip(TimelineClip clip)
        {
            var asset = clip.asset as GameAudioClip;
            var sound = asset != null ? asset.sound : null;
            if (sound == null || sound.Clips == null) return null;
            for (int i = 0; i < sound.Clips.Length; i++)
                if (sound.Clips[i] != null) return sound.Clips[i];
            return null;
        }

        private static bool ShouldLoop(TimelineClip clip)
        {
            var asset = clip.asset as GameAudioClip;
            if (asset == null) return false;
            return asset.loop || (asset.sound != null && asset.sound.Loop);
        }

        private static void PlayPreview(AudioClip clip, int startSample, bool loop)
        {
            PlayMethod?.Invoke(null, new object[] { clip, startSample, loop });
        }

        private static void StopIfPreviewing()
        {
            if (_currentAudio == null && _currentClip == null) return;
            StopAll();
            _currentAudio = null;
            _currentClip = null;
        }

        private static void StopAll() => StopAllMethod?.Invoke(null, null);

        private static MethodInfo Find(Type type, string[] names, Type[] signature)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string name in names)
            {
                MethodInfo method = type.GetMethod(name, flags, null, signature, null);
                if (method != null) return method;
            }
            return null;
        }
    }
}
