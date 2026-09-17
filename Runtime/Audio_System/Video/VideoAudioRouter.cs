using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Video;

namespace Kaddumi.UnityTools.Audio.Video
{
    /// <summary>
    /// Routes a <see cref="VideoPlayer"/>'s audio through the audio system's mixer buses, so a
    /// video's sound obeys the same per-bus volume, mute, and ducking as every other sound in the
    /// game. Drop this next to a <see cref="VideoPlayer"/> and pick the target <see cref="AudioBus"/>.
    ///
    /// <para>Under the hood it switches the player to <see cref="VideoAudioOutputMode.AudioSource"/>
    /// and points each of the clip's audio tracks at an <see cref="AudioSource"/> routed to the
    /// chosen bus's <see cref="AudioMixerGroup"/> (resolved from the <see cref="AudioManager"/>'s
    /// <see cref="Data.AudioConfig"/>). Game code still drives playback through the VideoPlayer
    /// itself (<c>Play</c>/<c>Pause</c>/<c>Stop</c>); this component only owns audio routing — it
    /// never starts or stops the video.</para>
    ///
    /// <para><b>Setup:</b> add a <see cref="VideoPlayer"/> and this component to a GameObject, then
    /// choose the bus. An <see cref="AudioManager"/> must be present in the scene for the audio to
    /// reach the mixer; without one, the video still plays but its audio is unrouted (2D, full
    /// volume). Track 0 reuses an <see cref="AudioSource"/> already on this object if present;
    /// extra tracks get their own child sources.</para>
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    [AddComponentMenu("Kaddumi/Audio/Video Audio Router")]
    public class VideoAudioRouter : MonoBehaviour
    {
        [Header("Routing")]
        [Tooltip("Mixing bus the video's audio is routed through.")]
        [SerializeField] private AudioBus bus = AudioBus.SFX;

        [Tooltip("Base volume for the video's audio, multiplied on top of the bus volume (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Header("Ducking")]
        [Tooltip("Duck the configured duck bus (typically Music) while the video plays, releasing it " +
                 "when the video finishes or this component is disabled. Looping videos stay ducked " +
                 "until disabled.")]
        [SerializeField] private bool duckMusicWhilePlaying = false;

        private VideoPlayer videoPlayer;
        // One target AudioSource per controlled audio track, created lazily.
        private readonly List<AudioSource> trackSources = new List<AudioSource>();
        private bool ducked;

        /// <summary>The bus this router currently sends the video's audio to.</summary>
        public AudioBus Bus => bus;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            // Must be set before the player prepares for the audio tracks to be captured.
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        }

        private void OnEnable()
        {
            videoPlayer.prepareCompleted += HandlePrepareCompleted;
            videoPlayer.started += HandleStarted;
            videoPlayer.loopPointReached += HandleEnded;

            // Re-enabled after a prepare (e.g. toggled off/on): rewire immediately.
            if (videoPlayer.isPrepared) ApplyRouting();
        }

        private void Start()
        {
            // By Start every Awake has run, so AudioManager.Instance is available. Wire up track 0
            // early (before the user's Play call) even though extra tracks are only known post-prepare.
            ApplyRouting();
        }

        private void OnDisable()
        {
            videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            videoPlayer.started -= HandleStarted;
            videoPlayer.loopPointReached -= HandleEnded;
            ReleaseDuck();
        }

        // --- Public API ------------------------------------------------------

        /// <summary>Reroutes the video's audio to a different bus, applying immediately if the player is set up.</summary>
        public void SetBus(AudioBus newBus)
        {
            bus = newBus;
            ApplyRouting();
        }

        /// <summary>Sets the video's base volume (0..1), multiplied on top of the bus volume.</summary>
        public void SetVolume(float linear01)
        {
            volume = Mathf.Clamp01(linear01);
            for (int i = 0; i < trackSources.Count; i++)
            {
                if (trackSources[i] != null) trackSources[i].volume = volume;
            }
        }

        // --- Routing ---------------------------------------------------------

        private void HandlePrepareCompleted(VideoPlayer vp) => ApplyRouting();

        private void ApplyRouting()
        {
            if (videoPlayer == null) return;

            AudioMixerGroup group = ResolveGroup();

            // controlledAudioTrackCount is 0 until the player has prepared; still wire track 0 so
            // a single-track video (the common case) is routed before playback begins.
            int trackCount = videoPlayer.controlledAudioTrackCount;
            if (trackCount < 1) trackCount = 1;

            // EnableAudioTrack only applies on the next prepare, so it's a no-op once prepared.
            bool canEnableTracks = !videoPlayer.isPrepared;

            for (ushort i = 0; i < trackCount; i++)
            {
                AudioSource source = GetOrCreateTrackSource(i);
                source.outputAudioMixerGroup = group;
                source.volume = volume;
                if (canEnableTracks) videoPlayer.EnableAudioTrack(i, true);
                videoPlayer.SetTargetAudioSource(i, source);
            }
        }

        private AudioMixerGroup ResolveGroup()
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogWarning($"[VideoAudioRouter] No AudioManager in the scene; '{name}' video " +
                                 "audio will play unrouted (2D, full volume).", this);
                return null;
            }

            AudioMixerGroup group = AudioManager.Instance.GetBusGroup(bus);
            if (group == null)
            {
                Debug.LogWarning($"[VideoAudioRouter] Bus '{bus}' has no AudioMixerGroup in the " +
                                 $"AudioConfig; '{name}' video audio won't be routed through the mixer.", this);
            }
            return group;
        }

        private AudioSource GetOrCreateTrackSource(int index)
        {
            while (trackSources.Count <= index)
            {
                AudioSource source;
                // Reuse an AudioSource already on this object for track 0; give extra tracks children.
                if (trackSources.Count == 0 && TryGetComponent(out AudioSource existing))
                {
                    source = existing;
                }
                else
                {
                    var go = new GameObject($"VideoAudioTrack_{trackSources.Count}");
                    go.transform.SetParent(transform, false);
                    source = go.AddComponent<AudioSource>();
                }
                source.playOnAwake = false;
                source.spatialBlend = 0f; // video audio is 2D by default
                trackSources.Add(source);
            }
            return trackSources[index];
        }

        // --- Ducking ---------------------------------------------------------

        private void HandleStarted(VideoPlayer vp)
        {
            if (!duckMusicWhilePlaying || ducked || AudioManager.Instance == null) return;
            // Held duck (no hold time) — released explicitly when the video ends or we disable.
            AudioManager.Instance.DuckMusicForVoice();
            ducked = true;
        }

        private void HandleEnded(VideoPlayer vp) => ReleaseDuck();

        private void ReleaseDuck()
        {
            if (!ducked) return;
            ducked = false;
            if (AudioManager.Instance != null) AudioManager.Instance.UnduckMusic();
        }

        private void OnValidate()
        {
            // Reflect volume tweaks made in the inspector at runtime on already-created sources.
            if (Application.isPlaying) SetVolume(volume);
        }
    }
}
