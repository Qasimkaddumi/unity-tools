using System;
using System.Collections;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using Kaddumi.UnityTools.Audio.Interfaces;
using UnityEngine;
using UnityEngine.Audio;

namespace Kaddumi.UnityTools.Audio.Providers.UnityAudio
{
    /// <summary>
    /// Default <see cref="IAudioProvider"/> backed by Unity's built-in AudioSource/AudioMixer.
    /// Self-contained like <c>AdMobProvider</c>: it creates its own <c>DontDestroyOnLoad</c>
    /// host GameObject that carries the SFX voice pool (also the coroutine runner) and the two
    /// music sources used for A/B crossfading. Bus volumes are applied by writing exposed mixer
    /// parameters in decibels.
    ///
    /// <para>Note: <see cref="PlayMusic"/> returns <see cref="AudioHandle.Invalid"/> — music is a
    /// singleton stream controlled via <see cref="StopMusic"/> and the Music bus, not per-voice.</para>
    /// </summary>
    public class UnityAudioProvider : IAudioProvider
    {
        private AudioConfig _config;
        private GameObject _host;
        private AudioVoicePool _pool;

        // Music crossfade uses two dedicated sources, ping-ponged on each PlayMusic.
        private AudioSource _musicA;
        private AudioSource _musicB;
        private bool _musicAActive;
        private Coroutine _musicRoutine;

        // Per-bus state (linear 0..1 and mute), mirrored into the mixer.
        private readonly Dictionary<AudioBus, float> _busVolume = new Dictionary<AudioBus, float>();
        private readonly Dictionary<AudioBus, bool> _busMuted = new Dictionary<AudioBus, bool>();
        private readonly Dictionary<AudioBus, Coroutine> _duckRoutines = new Dictionary<AudioBus, Coroutine>();

        public void Initialize(AudioConfig config, Action onComplete)
        {
            _config = config;

            _host = new GameObject("[Audio] UnityAudioProvider");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _pool = _host.AddComponent<AudioVoicePool>();
            _pool.Configure(
                config != null ? config.InitialPoolSize : 16,
                config != null ? config.MaxPoolSize : 48);

            CreateMusicSources();
            ApplyDefaultBusVolumes();

            onComplete?.Invoke();
        }

        private void CreateMusicSources()
        {
            AudioMixerGroup musicGroup = _config != null ? _config.GetBinding(AudioBus.Music)?.Group : null;
            _musicA = NewMusicSource("MusicA", musicGroup);
            _musicB = NewMusicSource("MusicB", musicGroup);
            _musicAActive = true;
        }

        private AudioSource NewMusicSource(string name, AudioMixerGroup group)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_host.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f; // music is always 2D
            src.volume = 0f;
            if (group != null) src.outputAudioMixerGroup = group;
            return src;
        }

        private void ApplyDefaultBusVolumes()
        {
            if (_config == null || _config.Buses == null) return;
            foreach (var binding in _config.Buses)
            {
                if (binding == null) continue;
                _busVolume[binding.Bus] = binding.DefaultVolume;
                _busMuted[binding.Bus] = false;
                ApplyBusToMixer(binding.Bus);
            }
        }

        // --- Bus volume ------------------------------------------------------

        public void SetBusVolume(AudioBus bus, float linear01)
        {
            _busVolume[bus] = Mathf.Clamp01(linear01);
            ApplyBusToMixer(bus);
        }

        public void SetBusMuted(AudioBus bus, bool muted)
        {
            _busMuted[bus] = muted;
            ApplyBusToMixer(bus);
        }

        private float GetStoredVolume(AudioBus bus) =>
            _busVolume.TryGetValue(bus, out var v) ? v : 1f;

        private bool IsMuted(AudioBus bus) =>
            _busMuted.TryGetValue(bus, out var m) && m;

        private void ApplyBusToMixer(AudioBus bus)
        {
            if (_config == null) return;
            // A duck coroutine owns the mixer param while it runs; don't fight it.
            if (_duckRoutines.TryGetValue(bus, out var running) && running != null) return;
            WriteBusDecibels(bus, IsMuted(bus) ? _config.MinDecibels : _config.LinearToDecibels(GetStoredVolume(bus)));
        }

        private void WriteBusDecibels(AudioBus bus, float decibels)
        {
            if (_config == null || _config.Mixer == null) return;
            var binding = _config.GetBinding(bus);
            if (binding == null || string.IsNullOrEmpty(binding.ExposedVolumeParam)) return;
            _config.Mixer.SetFloat(binding.ExposedVolumeParam, decibels);
        }

        // Current mixer value for a bus, so ramps start from where the bus actually is
        // (may be a ducked level, not the stored volume). Falls back to the stored volume.
        private float ReadBusDecibels(AudioBus bus)
        {
            if (_config != null && _config.Mixer != null)
            {
                var binding = _config.GetBinding(bus);
                if (binding != null && !string.IsNullOrEmpty(binding.ExposedVolumeParam) &&
                    _config.Mixer.GetFloat(binding.ExposedVolumeParam, out float current))
                {
                    return current;
                }
            }
            return _config != null ? _config.LinearToDecibels(GetStoredVolume(bus)) : 0f;
        }

        // --- SFX -------------------------------------------------------------

        public AudioHandle PlaySfx(SoundDefinition def, Vector3 position, bool spatial)
        {
            if (def == null) return AudioHandle.Invalid;
            var clip = def.PickClip();
            if (clip == null)
            {
                Debug.LogWarning($"[UnityAudioProvider] Sound '{def.Id}' has no clip assigned.");
                return AudioHandle.Invalid;
            }

            // Per-sound voice limit: steal the oldest instance of this sound before acquiring.
            if (def.MaxVoices > 0 && _pool.CountActive(def.Id) >= def.MaxVoices)
            {
                int steal = _pool.OldestActiveIndexFor(def.Id);
                if (steal >= 0) _pool.Release(steal);
            }

            var voice = _pool.Acquire(def.Bus, def.Id, out int index);
            var src = voice.Source;

            src.clip = clip;
            src.loop = def.Loop;
            src.volume = def.Volume;
            src.pitch = def.PickPitch();
            src.priority = def.Priority;
            src.outputAudioMixerGroup = _config != null ? _config.GetBinding(def.Bus)?.Group : null;

            bool use3D = spatial && def.SpatialBlend > 0f;
            src.spatialBlend = use3D ? def.SpatialBlend : 0f;
            if (use3D)
            {
                src.transform.position = position;
                src.minDistance = def.MinDistance;
                src.maxDistance = def.MaxDistance;
                src.rolloffMode = AudioRolloffMode.Linear;
            }
            else
            {
                src.transform.localPosition = Vector3.zero;
            }

            src.Play();
            return new AudioHandle(index, voice.Generation);
        }

        // --- Music -----------------------------------------------------------

        public AudioHandle PlayMusic(SoundDefinition def, float fadeSeconds)
        {
            if (def == null) return AudioHandle.Invalid;
            var clip = def.PickClip();
            if (clip == null)
            {
                Debug.LogWarning($"[UnityAudioProvider] Music '{def.Id}' has no clip assigned.");
                return AudioHandle.Invalid;
            }

            if (fadeSeconds < 0f) fadeSeconds = _config != null ? _config.DefaultMusicFadeSeconds : 1.5f;

            AudioSource incoming = _musicAActive ? _musicB : _musicA;
            AudioSource outgoing = _musicAActive ? _musicA : _musicB;
            _musicAActive = !_musicAActive;

            incoming.clip = clip;
            incoming.loop = true; // music always loops; use StopMusic to end it
            incoming.pitch = def.PickPitch();
            incoming.volume = 0f;
            incoming.Play();

            if (_musicRoutine != null) _pool.StopCoroutine(_musicRoutine);
            _musicRoutine = _pool.StartCoroutine(CrossfadeMusic(incoming, outgoing, def.Volume, fadeSeconds));

            return AudioHandle.Invalid; // music is controlled via StopMusic, not a voice handle
        }

        public void StopMusic(float fadeSeconds)
        {
            if (fadeSeconds < 0f) fadeSeconds = _config != null ? _config.DefaultMusicFadeSeconds : 1.5f;
            AudioSource active = _musicAActive ? _musicA : _musicB;
            AudioSource other = _musicAActive ? _musicB : _musicA;
            if (_musicRoutine != null) _pool.StopCoroutine(_musicRoutine);
            _musicRoutine = _pool.StartCoroutine(CrossfadeMusic(null, active, 0f, fadeSeconds));
            // Ensure the previously-outgoing source is fully silenced too.
            if (other != null) { other.Stop(); other.volume = 0f; }
        }

        private IEnumerator CrossfadeMusic(AudioSource incoming, AudioSource outgoing, float targetVolume, float seconds)
        {
            float inStart = incoming != null ? incoming.volume : 0f;
            float outStart = outgoing != null ? outgoing.volume : 0f;

            if (seconds <= 0f)
            {
                if (incoming != null) incoming.volume = targetVolume;
                if (outgoing != null) { outgoing.Stop(); outgoing.volume = 0f; }
                _musicRoutine = null;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                if (incoming != null) incoming.volume = Mathf.Lerp(inStart, targetVolume, k);
                if (outgoing != null) outgoing.volume = Mathf.Lerp(outStart, 0f, k);
                yield return null;
            }

            if (incoming != null) incoming.volume = targetVolume;
            if (outgoing != null) { outgoing.Stop(); outgoing.volume = 0f; }
            _musicRoutine = null;
        }

        // --- Ducking ---------------------------------------------------------

        public void Duck(AudioBus bus, float toLinear01, float attackSeconds, float releaseSeconds, float holdSeconds)
        {
            if (_config == null || _config.Mixer == null) return;
            if (_duckRoutines.TryGetValue(bus, out var running) && running != null) _pool.StopCoroutine(running);
            _duckRoutines[bus] = _pool.StartCoroutine(DuckRoutine(bus, Mathf.Clamp01(toLinear01), attackSeconds, releaseSeconds, holdSeconds));
        }

        private IEnumerator DuckRoutine(AudioBus bus, float toLinear01, float attack, float release, float hold)
        {
            float fromDb = ReadBusDecibels(bus);
            float toDb = _config.LinearToDecibels(toLinear01);

            yield return RampBus(bus, fromDb, toDb, attack);

            if (hold > 0f)
            {
                float held = 0f;
                while (held < hold) { held += Time.unscaledDeltaTime; yield return null; }

                // Release back to whatever the bus volume is now (user may have changed it).
                float restoreDb = IsMuted(bus) ? _config.MinDecibels : _config.LinearToDecibels(GetStoredVolume(bus));
                yield return RampBus(bus, toDb, restoreDb, release);

                _duckRoutines[bus] = null;
                ApplyBusToMixer(bus); // snap to exact stored value
            }
            else
            {
                // Stay ducked until the next Duck call restores the bus.
                _duckRoutines[bus] = null;
            }
        }

        private IEnumerator RampBus(AudioBus bus, float fromDb, float toDb, float seconds)
        {
            if (seconds <= 0f)
            {
                WriteBusDecibels(bus, toDb);
                yield break;
            }
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                WriteBusDecibels(bus, Mathf.Lerp(fromDb, toDb, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            WriteBusDecibels(bus, toDb);
        }

        // --- Voice control ---------------------------------------------------

        public void Stop(AudioHandle handle, float fadeSeconds)
        {
            if (!_pool.TryResolve(handle, out var voice, out int index)) return;
            if (fadeSeconds <= 0f)
            {
                _pool.Release(index);
                return;
            }
            voice.Fading = true;
            _pool.StartCoroutine(FadeOutVoice(voice, index, fadeSeconds));
        }

        private IEnumerator FadeOutVoice(AudioVoicePool.Voice voice, int index, float seconds)
        {
            int generation = voice.Generation;
            float start = voice.Source.volume;
            float t = 0f;
            while (t < seconds)
            {
                // Bail if the voice was recycled out from under us.
                if (!voice.Active || voice.Generation != generation) yield break;
                t += Time.unscaledDeltaTime;
                voice.Source.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            if (voice.Active && voice.Generation == generation) _pool.Release(index);
        }

        public void StopBus(AudioBus bus)
        {
            _pool.StopBus(bus);
            if (bus == AudioBus.Music) StopMusic(0f);
        }

        public void SetPaused(bool paused)
        {
            _pool.SetPaused(paused);
            if (_musicA != null) { if (paused) _musicA.Pause(); else _musicA.UnPause(); }
            if (_musicB != null) { if (paused) _musicB.Pause(); else _musicB.UnPause(); }
        }
    }
}
