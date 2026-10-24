using System.Collections;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace Kaddumi.UnityTools.Audio.Providers.UnityAudio
{
    /// <summary>
    /// Plays layered music (<see cref="MusicStemSet"/>) for <see cref="UnityAudioProvider"/>. Lives on
    /// the provider's <c>DontDestroyOnLoad</c> host GameObject. Every stem is its own
    /// <see cref="AudioSource"/>, all started with a single scheduled DSP time so the layers stay
    /// sample-accurate in sync, and each stem fades independently while all keep playing.
    ///
    /// <para>Two groups of sources (A and B) are ping-ponged on each <see cref="PlayStems"/>, like the
    /// provider's A/B music sources, so a new set crossfades in over the old one. Stem sources are
    /// created lazily as children of the host and never come from the SFX pool. All fades run in
    /// <see cref="Update"/> with unscaled time, matching the provider's music crossfade.</para>
    /// </summary>
    internal class MusicStemPlayer : MonoBehaviour
    {
        // Seconds between scheduling and the actual start, so every source is queued before it begins.
        private const double ScheduleLeadSeconds = 0.1;

        // Give up waiting on a clip that never reports Loaded/Failed (e.g. LoadAudioData refused).
        private const float LoadTimeoutSeconds = 10f;

        /// <summary>A value that eases linearly toward a target; a new <see cref="Begin"/> replaces any running fade.</summary>
        private sealed class FadeValue
        {
            public float Value;
            public bool Running;
            private float _from;
            private float _to;
            private float _duration;
            private float _elapsed;

            public void Snap(float value)
            {
                Value = value;
                _to = value;
                Running = false;
            }

            public void Begin(float to, float seconds)
            {
                if (seconds <= 0f)
                {
                    Snap(to);
                    return;
                }
                _from = Value;
                _to = to;
                _duration = seconds;
                _elapsed = 0f;
                Running = true;
            }

            public void Tick(float deltaTime)
            {
                if (!Running) return;
                _elapsed += deltaTime;
                float k = Mathf.Clamp01(_elapsed / _duration);
                Value = Mathf.Lerp(_from, _to, k);
                if (k >= 1f)
                {
                    Value = _to;
                    Running = false;
                }
            }
        }

        private sealed class Group
        {
            public string Prefix;
            public readonly List<AudioSource> Sources = new List<AudioSource>();
            public readonly List<FadeValue> Levels = new List<FadeValue>();
            public readonly FadeValue Gain = new FadeValue();
            public MusicStemSet Set;
            public bool Idle = true;      // true when no set is assigned
            public bool Scheduled;        // true once the sources have been started
            public bool Outgoing;         // true while fading out to be stopped
            public double StartAt;        // dsp time the sources were scheduled to start
            public Coroutine LoadRoutine; // pending load-then-schedule coroutine
        }

        private readonly Group _a = new Group { Prefix = "StemA" };
        private readonly Group _b = new Group { Prefix = "StemB" };
        private Group _active;
        private AudioMixerGroup _mixerGroup;
        private bool _paused;

        /// <summary>True while a stem set is current (loading, playing, or fading in).</summary>
        internal bool IsPlaying => _active != null && !_active.Idle;

        /// <summary>Sets the mixer group (the Music bus) that newly created stem sources route to.</summary>
        internal void Configure(AudioMixerGroup musicGroup)
        {
            _mixerGroup = musicGroup;
        }

        // --- Playback --------------------------------------------------------

        /// <summary>
        /// Crossfades to a new stem set over <paramref name="fadeSeconds"/>. The incoming sources start
        /// together at one scheduled DSP time once every clip has finished loading, and the fade-in
        /// begins at that moment so load time doesn't eat it.
        /// </summary>
        internal void PlayStems(MusicStemSet set, float fadeSeconds)
        {
            if (set == null || set.Stems == null || set.Stems.Length == 0)
            {
                Debug.LogWarning("[MusicStemPlayer] Cannot play a null or empty stem set.");
                return;
            }

            // Fade out whatever is current (a set still loading is simply cancelled).
            Group previous = _active;
            if (previous != null) RetireGroup(previous, fadeSeconds);
            _active = null;

            Group incoming = previous == _a ? _b : previous == _b ? _a : (_a.Idle ? _a : _b);
            if (!incoming.Idle) StopGroup(incoming); // an older crossfade is still fading out; hard cut it

            int count = set.Stems.Length;
            EnsureSources(incoming, count);

            incoming.Set = set;
            incoming.Idle = false;
            incoming.Scheduled = false;
            incoming.Outgoing = false;
            incoming.Gain.Snap(0f);

            for (int i = 0; i < count; i++)
            {
                var stem = set.Stems[i];
                var src = incoming.Sources[i];
                src.clip = stem != null ? stem.Clip : null;
                src.loop = true;
                src.pitch = 1f;
                src.volume = 0f;
                incoming.Levels[i].Snap(stem != null ? stem.StartVolume : 0f);
                if (src.clip != null) src.clip.LoadAudioData();
            }

            _active = incoming;
            incoming.LoadRoutine = StartCoroutine(LoadAndSchedule(incoming, fadeSeconds));
        }

        /// <summary>Fades one stem of the current set to <paramref name="linear01"/>; 0 or less snaps.</summary>
        internal void SetStemVolume(string stemId, float linear01, float fadeSeconds)
        {
            if (_active == null || _active.Idle)
            {
                Debug.LogWarning($"[MusicStemPlayer] No stem set is playing; cannot set stem '{stemId}'.");
                return;
            }

            int index = _active.Set.IndexOf(stemId);
            if (index < 0 || index >= _active.Levels.Count)
            {
                Debug.LogWarning($"[MusicStemPlayer] Stem '{stemId}' not found in set '{_active.Set.ResolvedId}'.");
                return;
            }

            _active.Levels[index].Begin(Mathf.Clamp01(linear01), fadeSeconds);
        }

        /// <summary>Fades out and stops the current set; also cancels a pending load.</summary>
        internal void StopStems(float fadeSeconds)
        {
            if (_active == null) return;
            RetireGroup(_active, fadeSeconds);
            _active = null;
        }

        /// <summary>Stops everything immediately, including sets that are fading out.</summary>
        internal void StopAll()
        {
            StopGroup(_a);
            StopGroup(_b);
            _active = null;
        }

        /// <summary>Pauses/resumes every stem source in both groups in the same call.</summary>
        internal void SetPaused(bool paused)
        {
            _paused = paused;

            if (!paused && _active != null && _active.Scheduled) SyncSources(_active);

            ApplyPause(_a, paused);
            ApplyPause(_b, paused);
        }

        private static void ApplyPause(Group group, bool paused)
        {
            for (int i = 0; i < group.Sources.Count; i++)
            {
                var src = group.Sources[i];
                if (src == null) continue;
                if (paused) src.Pause();
                else src.UnPause();
            }
        }

        // Re-aligns the stems to the first stem's playhead so a pause/resume can't leave them apart.
        private static void SyncSources(Group group)
        {
            AudioSource reference = null;
            for (int i = 0; i < group.Sources.Count; i++)
            {
                if (group.Sources[i] != null && group.Sources[i].clip != null)
                {
                    reference = group.Sources[i];
                    break;
                }
            }
            if (reference == null) return;

            int position = reference.timeSamples;
            for (int i = 0; i < group.Sources.Count; i++)
            {
                var src = group.Sources[i];
                if (src == null || src == reference || src.clip == null) continue;
                if (src.clip.samples != reference.clip.samples) continue;
                if (position < 0 || position >= src.clip.samples) continue;
                src.timeSamples = position;
            }
        }

        // --- Loading / scheduling --------------------------------------------

        private IEnumerator LoadAndSchedule(Group group, float fadeSeconds)
        {
            float waited = 0f;
            while (!AllClipsSettled(group))
            {
                waited += Time.unscaledDeltaTime;
                if (waited >= LoadTimeoutSeconds)
                {
                    Debug.LogWarning($"[MusicStemPlayer] Timed out waiting for the clips of '{group.Set.ResolvedId}' to load.");
                    break;
                }
                yield return null;
            }

            group.LoadRoutine = null;

            double startAt = AudioSettings.dspTime + ScheduleLeadSeconds;
            group.StartAt = startAt;
            for (int i = 0; i < group.Sources.Count; i++)
            {
                var src = group.Sources[i];
                if (src.clip == null) continue;

                if (!IsPlayable(src.clip))
                {
                    Debug.LogWarning($"[MusicStemPlayer] Clip '{src.clip.name}' failed to load; its stem will be silent.");
                    src.clip = null; // so SyncSources never uses an unplayed source as its reference
                    continue;
                }

                src.PlayScheduled(startAt);
                if (_paused) src.Pause();
            }

            group.Scheduled = true;
            group.Gain.Begin(1f, fadeSeconds);
        }

        // Streaming clips never report Loaded, so they count as ready immediately.
        private static bool AllClipsSettled(Group group)
        {
            for (int i = 0; i < group.Sources.Count; i++)
            {
                var clip = group.Sources[i].clip;
                if (clip == null || clip.loadType == AudioClipLoadType.Streaming) continue;
                var state = clip.loadState;
                if (state != AudioDataLoadState.Loaded && state != AudioDataLoadState.Failed) return false;
            }
            return true;
        }

        private static bool IsPlayable(AudioClip clip)
        {
            if (clip.loadType == AudioClipLoadType.Streaming) return true;
            return clip.loadState == AudioDataLoadState.Loaded;
        }

        // --- Group lifecycle -------------------------------------------------

        // Fades a group out and stops it. Not-yet-started or instant fades stop immediately.
        private void RetireGroup(Group group, float fadeSeconds)
        {
            if (group.LoadRoutine != null)
            {
                StopCoroutine(group.LoadRoutine);
                group.LoadRoutine = null;
            }

            if (!group.Scheduled || fadeSeconds <= 0f)
            {
                StopGroup(group);
                return;
            }

            group.Outgoing = true;
            group.Gain.Begin(0f, fadeSeconds);
        }

        private void StopGroup(Group group)
        {
            if (group.LoadRoutine != null)
            {
                StopCoroutine(group.LoadRoutine);
                group.LoadRoutine = null;
            }

            for (int i = 0; i < group.Sources.Count; i++)
            {
                var src = group.Sources[i];
                if (src == null) continue;
                src.Stop();
                src.clip = null;
                src.volume = 0f;
            }

            group.Set = null;
            group.Idle = true;
            group.Scheduled = false;
            group.Outgoing = false;
            group.Gain.Snap(0f);
            if (_active == group) _active = null;
        }

        // Grows the group to at least `count` sources (never shrinks) and level slots to match.
        private void EnsureSources(Group group, int count)
        {
            while (group.Sources.Count < count)
            {
                var go = new GameObject($"{group.Prefix}_{group.Sources.Count}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f; // music is always 2D
                src.pitch = 1f;
                src.volume = 0f;
                if (_mixerGroup != null) src.outputAudioMixerGroup = _mixerGroup;
                group.Sources.Add(src);
            }

            while (group.Levels.Count < group.Sources.Count) group.Levels.Add(new FadeValue());
        }

        // --- Fades -----------------------------------------------------------

        private void Update()
        {
            TickGroup(_a);
            TickGroup(_b);
        }

        private void TickGroup(Group group)
        {
            if (group.Idle) return;

            float dt = Time.unscaledDeltaTime;
            group.Gain.Tick(dt);

            if (group.Outgoing && !group.Gain.Running)
            {
                StopGroup(group);
                return;
            }

            float master = group.Set != null ? group.Set.Volume : 1f;
            int active = group.Set != null && group.Set.Stems != null ? group.Set.Stems.Length : 0;
            for (int i = 0; i < group.Sources.Count; i++)
            {
                var src = group.Sources[i];
                if (src == null) continue;
                if (i >= active)
                {
                    src.volume = 0f;
                    continue;
                }
                var level = group.Levels[i];
                level.Tick(dt);
                src.volume = level.Value * master * group.Gain.Value;
            }
        }
    }
}
