using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Providers.UnityAudio
{
    /// <summary>
    /// Runtime host and object pool for SFX <see cref="AudioSource"/> voices, owned by
    /// <see cref="UnityAudioProvider"/> on a <c>DontDestroyOnLoad</c> GameObject. Also serves
    /// as the provider's coroutine runner (fades, ducking) since a MonoBehaviour is required.
    ///
    /// <para>Each pooled voice carries a generation counter that increments on every acquire,
    /// so an <see cref="AudioHandle"/> handed out earlier can be validated against the voice's
    /// current generation — this is what makes stopping a recycled voice a safe no-op.</para>
    /// </summary>
    internal class AudioVoicePool : MonoBehaviour
    {
        internal class Voice
        {
            public AudioSource Source;
            public int Generation;
            public bool Active;
            public bool Fading;      // provider is running a fade coroutine on this voice
            public float StartTime;  // unscaled time the voice was last acquired (for voice stealing)
            public AudioBus Bus;
            public SoundDefinition Sound; // for per-sound MaxVoices accounting
            public Coroutine FadeRoutine; // provider-run volume fade (FadeVoice); null when none
            public Transform Follow;      // object this voice tracks; meaningful while HasFollow
            public bool HasFollow;        // kept apart from Follow so a destroyed target can be told from no target
            public bool Scheduled;        // started with PlayScheduled at ScheduledStart
            public double ScheduledStart;
            public bool Held;             // scheduled start not reached when paused: stopped until resume
            public bool HasScheduledEnd;
            public double ScheduledEnd;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private readonly Dictionary<SoundDefinition, int> _activeCounts = new Dictionary<SoundDefinition, int>();
        private int _maxSize;
        private bool _priorityStealing;
        private bool _paused;
        private double _pausedAt;

        internal IReadOnlyList<Voice> Voices => _voices;

        /// <summary>Raised when a voice reaches its natural end, not when it is stopped or stolen.</summary>
        internal event Action<AudioHandle> VoiceFinished;

        internal void Configure(int initialSize, int maxSize, bool priorityStealing = false)
        {
            _maxSize = Mathf.Max(1, maxSize);
            _priorityStealing = priorityStealing;
            int initial = Mathf.Clamp(initialSize, 1, _maxSize);
            for (int i = 0; i < initial; i++) CreateVoice();
        }

        private Voice CreateVoice()
        {
            var go = new GameObject($"SfxVoice_{_voices.Count}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            var voice = new Voice { Source = src, Generation = 0, Active = false };
            _voices.Add(voice);
            return voice;
        }

        /// <summary>
        /// Acquires a voice for a sound, growing the pool up to the cap and otherwise stealing
        /// the least important active voice (one-shots before loops, then the lowest Unity priority,
        /// then the oldest). Bumps the returned voice's generation and marks it active. With priority
        /// stealing on, only voices of the same or lower priority can be stolen, and this returns null
        /// (index -1) when there are none.
        /// </summary>
        internal Voice Acquire(SoundDefinition sound, out int index)
        {
            // 1) Reuse a free voice.
            for (int i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].Active)
                {
                    index = i;
                    return Activate(_voices[i], sound);
                }
            }

            // 2) Grow if we're still under the cap.
            if (_voices.Count < _maxSize)
            {
                var voice = CreateVoice();
                index = _voices.Count - 1;
                return Activate(voice, sound);
            }

            // 3) Steal the least important active voice.
            index = _priorityStealing ? PriorityVictimIndex(sound.Priority) : StealVictimIndex();
            if (index < 0) return null;
            var stolen = _voices[index];
            stolen.Source.Stop();
            stolen.Fading = false;
            if (stolen.FadeRoutine != null)
            {
                StopCoroutine(stolen.FadeRoutine);
                stolen.FadeRoutine = null;
            }
            return Activate(stolen, sound);
        }

        private Voice Activate(Voice voice, SoundDefinition sound)
        {
            ClearPlayState(voice);
            voice.Generation++;
            voice.Active = true;
            voice.Fading = false;
            voice.StartTime = Time.unscaledTime;
            voice.Bus = sound.Bus;
            voice.Sound = sound;
            _activeCounts.TryGetValue(sound, out int count);
            _activeCounts[sound] = count + 1;
            return voice;
        }

        // Clears what belongs to the sound a voice was playing. Stolen voices come through here
        // without going through Release, so nothing from the old sound carries over.
        private void ClearPlayState(Voice voice)
        {
            if (!ReferenceEquals(voice.Sound, null) && _activeCounts.TryGetValue(voice.Sound, out int count))
            {
                if (count <= 1) _activeCounts.Remove(voice.Sound);
                else _activeCounts[voice.Sound] = count - 1;
            }
            voice.Sound = null;
            voice.Follow = null;
            voice.HasFollow = false;
            voice.Scheduled = false;
            voice.Held = false;
            voice.HasScheduledEnd = false;
        }

        // Victim order: non-looping before looping, then highest Source.priority number
        // (0 = most important), then oldest StartTime. Always returns a valid index.
        private int StealVictimIndex()
        {
            int victim = -1;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.Active) continue;
                if (victim < 0 || IsBetterVictim(v, _voices[victim])) victim = i;
            }
            return victim >= 0 ? victim : 0;
        }

        private static bool IsBetterVictim(Voice candidate, Voice current)
        {
            bool candLoop = candidate.Source != null && candidate.Source.loop;
            bool currLoop = current.Source != null && current.Source.loop;
            if (candLoop != currLoop) return !candLoop;

            int candPriority = candidate.Source != null ? candidate.Source.priority : 128;
            int currPriority = current.Source != null ? current.Source.priority : 128;
            if (candPriority != currPriority) return candPriority > currPriority;

            return candidate.StartTime < current.StartTime;
        }

        // Priority-stealing victim: highest Source.priority number (0 = most important), then oldest
        // StartTime, among voices no more important than the new sound. -1 when there are none.
        private int PriorityVictimIndex(int newPriority)
        {
            int victim = -1;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.Active || v.Source == null || v.Source.priority < newPriority) continue;
                if (victim < 0) { victim = i; continue; }

                var current = _voices[victim];
                if (v.Source.priority > current.Source.priority ||
                    (v.Source.priority == current.Source.priority && v.StartTime < current.StartTime))
                    victim = i;
            }
            return victim;
        }

        /// <summary>Returns a voice to the pool and silences its source.</summary>
        internal void Release(int index)
        {
            if (index < 0 || index >= _voices.Count) return;
            var voice = _voices[index];
            ClearPlayState(voice);
            voice.Active = false;
            voice.Fading = false;
            if (voice.FadeRoutine != null)
            {
                StopCoroutine(voice.FadeRoutine);
                voice.FadeRoutine = null;
            }
            if (voice.Source != null)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.loop = false;
            }
        }

        /// <summary>True when the handle still refers to the live voice it was issued for.</summary>
        internal bool TryResolve(AudioHandle handle, out Voice voice, out int index)
        {
            voice = null;
            index = handle.VoiceIndex;
            if (index < 0 || index >= _voices.Count) return false;
            var v = _voices[index];
            if (!v.Active || v.Generation != handle.Generation) return false;
            voice = v;
            return true;
        }

        /// <summary>
        /// True while the handle's voice is still sounding or waiting to: playing, paused, fading,
        /// loading, or scheduled but not started. False once it has ended, even before it is reclaimed.
        /// </summary>
        internal bool IsVoicePlaying(AudioHandle handle)
        {
            if (!TryResolve(handle, out var voice, out _) || voice.Source == null) return false;
            return _paused || !HasEnded(voice);
        }

        /// <summary>Counts active voices currently playing a given sound (for MaxVoices limits).</summary>
        internal int CountActive(SoundDefinition sound)
            => _activeCounts.TryGetValue(sound, out int count) ? count : 0;

        /// <summary>Index of the oldest active voice playing a given sound, or -1 if none.</summary>
        internal int OldestActiveIndexFor(SoundDefinition sound)
        {
            int oldest = -1;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Active && ReferenceEquals(_voices[i].Sound, sound) && _voices[i].StartTime < oldestTime)
                {
                    oldestTime = _voices[i].StartTime;
                    oldest = i;
                }
            }
            return oldest;
        }

        internal void StopBus(AudioBus bus)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Active && _voices[i].Bus == bus) Release(i);
            }
        }

        /// <summary>True if a voice in the pool currently holds this clip.</summary>
        internal bool IsClipInUse(AudioClip clip)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.Active && v.Source != null && v.Source.clip == clip) return true;
            }
            return false;
        }

        // --- Follow ----------------------------------------------------------

        /// <summary>Makes a voice track a target; a null target detaches it, leaving it where it is.</summary>
        internal void Attach(Voice voice, Transform target)
        {
            voice.HasFollow = target != null;
            voice.Follow = target;
            if (target != null) voice.Source.transform.position = target.position;
        }

        // In LateUpdate so a voice picks up where its target ended up this frame.
        private void LateUpdate()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.Active || !v.HasFollow || v.Source == null) continue;

                if (v.Follow == null) // destroyed
                {
                    v.Follow = null;
                    v.HasFollow = false;
                    if (v.Source.loop) Release(i); // otherwise it would never end
                    continue;
                }

                v.Source.transform.position = v.Follow.position;
            }
        }

        // --- Scheduling ------------------------------------------------------

        /// <summary>Starts a prepared voice at a DSP time. While paused, it is held until resume.</summary>
        internal void PlayScheduled(Voice voice, double dspTime)
        {
            voice.Scheduled = true;
            voice.ScheduledStart = ToPauseAdjustedTime(dspTime);
            if (_paused) voice.Held = true;
            else voice.Source.PlayScheduled(dspTime);
        }

        /// <summary>Ends a voice at a DSP time. This also ends a looping voice.</summary>
        internal void SetScheduledEnd(Voice voice, double dspTime)
        {
            voice.HasScheduledEnd = true;
            voice.ScheduledEnd = ToPauseAdjustedTime(dspTime);
            if (!voice.Held) voice.Source.SetScheduledEndTime(dspTime);
        }

        // DSP time keeps running during a pause, and SetPaused pushes scheduled times back by the
        // pause length on resume. A time given mid-pause is moved back by the part already elapsed,
        // so it stays the same distance from "now" once resumed.
        private double ToPauseAdjustedTime(double dspTime)
            => _paused ? dspTime - (AudioSettings.dspTime - _pausedAt) : dspTime;

        internal void SetPaused(bool paused)
        {
            bool changed = paused != _paused;
            double now = AudioSettings.dspTime;
            double pausedFor = changed && !paused ? now - _pausedAt : 0d;
            if (changed && paused) _pausedAt = now;
            _paused = paused;

            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.Active || v.Source == null) continue;

                if (paused)
                {
                    if (v.Held) continue;
                    // Pausing a source that hasn't started yet isn't reliable: stop it and reschedule on resume.
                    if (v.Scheduled && now < v.ScheduledStart)
                    {
                        v.Source.Stop();
                        v.Held = true;
                        continue;
                    }
                    v.Source.Pause();
                    continue;
                }

                if (v.Scheduled) v.ScheduledStart += pausedFor;
                if (v.HasScheduledEnd) v.ScheduledEnd += pausedFor;

                if (v.Held)
                {
                    v.Held = false;
                    v.Source.PlayScheduled(v.ScheduledStart);
                }
                else
                {
                    v.Source.UnPause();
                }

                if (changed && v.HasScheduledEnd) v.Source.SetScheduledEndTime(v.ScheduledEnd);
            }
        }

        // --- Reclaiming ------------------------------------------------------

        // A looping source only ends through a scheduled end. A source that isn't playing yet because
        // its scheduled start hasn't come or its clip is still loading hasn't ended either.
        private static bool HasEnded(Voice voice)
        {
            if (voice.Held) return false;
            var src = voice.Source;
            if (src.isPlaying) return false;
            if (src.loop && !voice.HasScheduledEnd) return false;
            if (src.clip != null && src.clip.loadState == AudioDataLoadState.Loading) return false;
            return !voice.Scheduled || AudioSettings.dspTime >= voice.ScheduledStart;
        }

        // Reclaim voices that reached their natural end. Skipped while paused (a paused source reports
        // !isPlaying) and while a fade coroutine is mid-flight (the provider releases those itself).
        private void Update()
        {
            if (_paused) return;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.Active && !v.Fading && v.Source != null && HasEnded(v))
                {
                    var handle = new AudioHandle(i, v.Generation);
                    Release(i);
                    VoiceFinished?.Invoke(handle);
                }
            }
        }
    }
}
