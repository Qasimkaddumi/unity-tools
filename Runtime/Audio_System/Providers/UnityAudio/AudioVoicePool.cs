using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Core;
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
            public string SoundId;   // for per-sound MaxVoices accounting
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private int _maxSize;
        private bool _paused;

        internal IReadOnlyList<Voice> Voices => _voices;

        internal void Configure(int initialSize, int maxSize)
        {
            _maxSize = Mathf.Max(1, maxSize);
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
        /// the oldest active voice. Bumps the returned voice's generation and marks it active.
        /// </summary>
        internal Voice Acquire(AudioBus bus, string soundId, out int index)
        {
            // 1) Reuse a free voice.
            for (int i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].Active)
                {
                    index = i;
                    return Activate(_voices[i], bus, soundId);
                }
            }

            // 2) Grow if we're still under the cap.
            if (_voices.Count < _maxSize)
            {
                var voice = CreateVoice();
                index = _voices.Count - 1;
                return Activate(voice, bus, soundId);
            }

            // 3) Steal the oldest active voice.
            index = OldestActiveIndex();
            var stolen = _voices[index];
            stolen.Source.Stop();
            stolen.Fading = false;
            return Activate(stolen, bus, soundId);
        }

        private Voice Activate(Voice voice, AudioBus bus, string soundId)
        {
            voice.Generation++;
            voice.Active = true;
            voice.Fading = false;
            voice.StartTime = Time.unscaledTime;
            voice.Bus = bus;
            voice.SoundId = soundId;
            return voice;
        }

        private int OldestActiveIndex()
        {
            int oldest = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Active && _voices[i].StartTime < oldestTime)
                {
                    oldestTime = _voices[i].StartTime;
                    oldest = i;
                }
            }
            return oldest;
        }

        /// <summary>Returns a voice to the pool and silences its source.</summary>
        internal void Release(int index)
        {
            if (index < 0 || index >= _voices.Count) return;
            var voice = _voices[index];
            voice.Active = false;
            voice.Fading = false;
            voice.SoundId = null;
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

        /// <summary>Counts active voices currently playing a given sound (for MaxVoices limits).</summary>
        internal int CountActive(string soundId)
        {
            int count = 0;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Active && _voices[i].SoundId == soundId) count++;
            }
            return count;
        }

        /// <summary>Index of the oldest active voice playing a given sound, or -1 if none.</summary>
        internal int OldestActiveIndexFor(string soundId)
        {
            int oldest = -1;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Active && _voices[i].SoundId == soundId && _voices[i].StartTime < oldestTime)
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

        internal void SetPaused(bool paused)
        {
            _paused = paused;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.Active || v.Source == null) continue;
                if (paused) v.Source.Pause();
                else v.Source.UnPause();
            }
        }

        // Reclaim finished one-shots. Skipped while paused (a paused source reports !isPlaying)
        // and while a fade coroutine is mid-flight (the provider releases those itself).
        private void Update()
        {
            if (_paused) return;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.Active && !v.Fading && v.Source != null && !v.Source.loop && !v.Source.isPlaying)
                {
                    Release(i);
                }
            }
        }
    }
}
