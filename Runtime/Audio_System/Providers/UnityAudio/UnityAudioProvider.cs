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
    /// <para>Note: PlayMusic returns <see cref="AudioHandle.Invalid"/> — music is a singleton stream
    /// controlled via <see cref="StopMusic"/> and the Music bus, not per-voice.</para>
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

        // The sound PlayMusic last started; null once it is stopped. Kept apart from isPlaying,
        // which also reads false while paused and while the clip loads.
        private SoundDefinition _currentMusic;
        private int _musicLoopCount;
        private int _lastMusicSamples;

        // Layered music (stems) is handled by its own player on the same host.
        private MusicStemPlayer _stems;

        private AudioClipLoader _clipLoader;
        private bool _paused;

        // Per-bus state (linear 0..1 and mute), mirrored into the mixer.
        private readonly Dictionary<AudioBus, float> _busVolume = new Dictionary<AudioBus, float>();
        private readonly Dictionary<AudioBus, bool> _busMuted = new Dictionary<AudioBus, bool>();
        private readonly Dictionary<AudioBus, Coroutine> _duckRoutines = new Dictionary<AudioBus, Coroutine>();

        public event Action<AudioHandle> VoiceFinished;

        public void Initialize(AudioConfig config, Action onComplete)
        {
            _config = config;

            _host = new GameObject("[Audio] UnityAudioProvider");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _pool = _host.AddComponent<AudioVoicePool>();
            _pool.Configure(
                config != null ? config.InitialPoolSize : 16,
                config != null ? config.MaxPoolSize : 48,
                config != null && config.PriorityStealing);
            _pool.VoiceFinished += handle => VoiceFinished?.Invoke(handle);

            CreateMusicSources();
            _stems = _host.AddComponent<MusicStemPlayer>();
            _stems.Configure(_config != null ? _config.GetBinding(AudioBus.Music)?.Group : null);
            _clipLoader = new AudioClipLoader(_pool, IsClipInUse);
            ApplyDefaultBusVolumes();
            _pool.StartCoroutine(TickRoutine());

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
            var options = new SoundPlayOptions { Position = position, Spatial = spatial };
            return PlaySfxWithOptions(def, options);
        }

        public AudioHandle PlaySfxWithOptions(SoundDefinition def, in SoundPlayOptions options)
        {
            var voice = PrepareVoice(def, options, out int index);
            if (voice == null) return AudioHandle.Invalid;

            voice.Source.Play();
            return new AudioHandle(index, voice.Generation);
        }

        public AudioHandle PlaySfxScheduled(SoundDefinition def, in SoundPlayOptions options, double dspTime)
        {
            var voice = PrepareVoice(def, options, out int index);
            if (voice == null) return AudioHandle.Invalid;

            _pool.PlayScheduled(voice, dspTime);
            return new AudioHandle(index, voice.Generation);
        }

        // Acquires a voice and sets it up for a sound, ready to start. Null if it can't play.
        private AudioVoicePool.Voice PrepareVoice(SoundDefinition def, in SoundPlayOptions options, out int index)
        {
            index = -1;
            if (def == null) return null;
            var clip = options.ClipOverride != null ? options.ClipOverride : def.PickClip();
            if (clip == null)
            {
                Debug.LogWarning($"[UnityAudioProvider] Sound '{def.Id}' has no clip assigned.");
                return null;
            }

            // Per-sound voice limit: refuse, or steal the oldest instance of this sound before acquiring.
            if (def.MaxVoices > 0 && _pool.CountActive(def) >= def.MaxVoices)
            {
                if (def.LimitMode == VoiceLimitMode.Refuse) return null;
                int steal = _pool.OldestActiveIndexFor(def);
                if (steal >= 0) _pool.Release(steal);
            }

            var voice = _pool.Acquire(def, out index);
            if (voice == null) return null;
            var src = voice.Source;

            // Loading up front lets the pool see the Loading state and not mistake the wait for an end.
            if (clip.loadType != AudioClipLoadType.Streaming && clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();

            src.clip = clip;
            src.loop = options.Loop ?? def.Loop;
            src.volume = options.Volume ?? def.Volume;
            src.pitch = options.Pitch ?? def.PickPitch();
            src.priority = def.Priority;
            src.outputAudioMixerGroup = _config != null ? _config.GetBinding(def.Bus)?.Group : null;

            bool use3D = options.Spatial && def.SpatialBlend > 0f;
            src.spatialBlend = use3D ? def.SpatialBlend : 0f;
            if (use3D)
            {
                src.transform.position = options.Position;
                src.minDistance = def.MinDistance;
                src.maxDistance = def.MaxDistance;
                ApplyRolloff(src, def);
            }
            else
            {
                src.transform.localPosition = Vector3.zero;
            }

            if (options.Follow != null)
            {
                if (!use3D) WarnFollowOn2D(def.Id);
                _pool.Attach(voice, options.Follow);
            }

            return voice;
        }

        private static void ApplyRolloff(AudioSource src, SoundDefinition def)
        {
            if (def.Rolloff != AudioRolloffMode.Custom)
            {
                src.rolloffMode = def.Rolloff;
                return;
            }

            if (def.RolloffCurve == null || def.RolloffCurve.length == 0)
            {
                Debug.LogWarning($"[UnityAudioProvider] Sound '{def.Id}' uses Custom rolloff but has no curve; using Linear.");
                src.rolloffMode = AudioRolloffMode.Linear;
                return;
            }

            src.rolloffMode = AudioRolloffMode.Custom;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, def.RolloffCurve);
        }

        private static void WarnFollowOn2D(string soundId)
        {
            Debug.LogWarning($"[UnityAudioProvider] Sound '{soundId}' is following an object but plays in 2D, so the movement won't be heard. " +
                             "Set Spatial on the play options and give the sound a SpatialBlend above 0.");
        }

        // --- Clip data -------------------------------------------------------

        public void LoadSounds(IReadOnlyList<SoundDefinition> sounds, Action<float> progress, Action done)
            => _clipLoader.Load(sounds, progress, done);

        public void UnloadSounds(IReadOnlyList<SoundDefinition> sounds) => _clipLoader.Unload(sounds);

        public bool IsLoaded(SoundDefinition sound) => AudioClipLoader.IsLoaded(sound);

        private bool IsClipInUse(AudioClip clip)
        {
            if (_pool.IsClipInUse(clip)) return true;
            if (_stems != null && _stems.IsClipInUse(clip)) return true;
            // The current track counts even before it starts; a source that was stopped keeps its clip, so otherwise check isPlaying.
            if (_currentMusic != null && ActiveMusicSource.clip == clip) return true;
            return (_musicA.isPlaying && _musicA.clip == clip) || (_musicB.isPlaying && _musicB.clip == clip);
        }

        // Runs for the provider's lifetime: per-frame work that isn't tied to a voice.
        private IEnumerator TickRoutine()
        {
            while (true)
            {
                if (!_paused)
                {
                    TickMusicLoopCount();
                    _clipLoader.ProcessPendingUnloads(); // paused sources read as not playing, so wait until resume
                }
                yield return null;
            }
        }

        // --- Music -----------------------------------------------------------

        private AudioSource ActiveMusicSource => _musicAActive ? _musicA : _musicB;

        public bool IsMusicPlaying => _currentMusic != null;

        public SoundDefinition CurrentMusic => _currentMusic != null ? _currentMusic : null;

        public float MusicTime => _currentMusic != null ? ActiveMusicSource.time : 0f;

        public float MusicLength
        {
            get
            {
                if (_currentMusic == null) return 0f;
                var clip = ActiveMusicSource.clip;
                return clip != null ? clip.length : 0f;
            }
        }

        public int MusicLoopCount => _currentMusic != null ? _musicLoopCount : 0;

        // Unity has no loop callback: the playhead moving backwards means the track wrapped.
        private void TickMusicLoopCount()
        {
            if (_currentMusic == null) return;
            var src = ActiveMusicSource;
            if (!src.isPlaying) return;
            int samples = src.timeSamples;
            if (samples < _lastMusicSamples) _musicLoopCount++;
            _lastMusicSamples = samples;
        }

        public AudioHandle PlayMusic(SoundDefinition def, float fadeSeconds) => PlayMusic(def, fadeSeconds, null);

        public AudioHandle PlayMusic(SoundDefinition def, float fadeSeconds, float? volume)
        {
            if (def == null) return AudioHandle.Invalid;
            var clip = def.PickClip();
            if (clip == null)
            {
                Debug.LogWarning($"[UnityAudioProvider] Music '{def.Id}' has no clip assigned.");
                return AudioHandle.Invalid;
            }

            if (fadeSeconds < 0f) fadeSeconds = _config != null ? _config.DefaultMusicFadeSeconds : 1.5f;

            // One music at a time: fade out any layered music first.
            if (_stems != null && _stems.IsPlaying) _stems.StopStems(fadeSeconds);

            AudioSource incoming = _musicAActive ? _musicB : _musicA;
            AudioSource outgoing = _musicAActive ? _musicA : _musicB;
            _musicAActive = !_musicAActive;

            incoming.clip = clip;
            incoming.loop = true; // music always loops; use StopMusic to end it
            incoming.pitch = def.PickPitch();
            incoming.volume = 0f;
            incoming.Play();

            _currentMusic = def;
            _musicLoopCount = 0;
            _lastMusicSamples = 0;

            if (_musicRoutine != null) _pool.StopCoroutine(_musicRoutine);
            _musicRoutine = _pool.StartCoroutine(CrossfadeMusic(incoming, outgoing, volume ?? def.Volume, fadeSeconds));

            return AudioHandle.Invalid; // music is controlled via StopMusic, not a voice handle
        }

        public void StopMusic(float fadeSeconds)
        {
            if (fadeSeconds < 0f) fadeSeconds = _config != null ? _config.DefaultMusicFadeSeconds : 1.5f;
            StopMusicSources(fadeSeconds);
            if (_stems != null) _stems.StopStems(fadeSeconds);
        }

        // Fades out the regular A/B music sources only (not stems). Expects a resolved fade.
        private void StopMusicSources(float fadeSeconds)
        {
            _currentMusic = null;
            AudioSource active = _musicAActive ? _musicA : _musicB;
            AudioSource other = _musicAActive ? _musicB : _musicA;
            if (_musicRoutine != null) _pool.StopCoroutine(_musicRoutine);
            _musicRoutine = _pool.StartCoroutine(CrossfadeMusic(null, active, 0f, fadeSeconds));
            // Ensure the previously-outgoing source is fully silenced too.
            if (other != null) { other.Stop(); other.volume = 0f; }
        }

        // --- Music stems -----------------------------------------------------

        /// <summary>
        /// Crossfades to a stem set. Calling it with the set that is already playing does nothing;
        /// call StopStems(0) first to restart it.
        /// </summary>
        public void PlayStems(MusicStemSet set, float fadeSeconds)
        {
            if (_stems == null) return;
            if (set == null)
            {
                Debug.LogWarning("[UnityAudioProvider] Cannot play a null music stem set.");
                return;
            }

            fadeSeconds = ResolveMusicFade(fadeSeconds);

            _currentMusic = null;

            // One music at a time: fade out regular A/B music if it's playing.
            if ((_musicA != null && (_musicA.isPlaying || _musicA.volume > 0f)) ||
                (_musicB != null && (_musicB.isPlaying || _musicB.volume > 0f)))
                StopMusicSources(fadeSeconds);

            _stems.PlayStems(set, fadeSeconds);
        }

        public void SetStemVolume(string stemId, float linear01, float fadeSeconds)
        {
            if (_stems == null) return;
            _stems.SetStemVolume(stemId, linear01, ResolveMusicFade(fadeSeconds));
        }

        public void StopStems(float fadeSeconds)
        {
            if (_stems == null) return;
            _stems.StopStems(ResolveMusicFade(fadeSeconds));
        }

        public string CurrentStemSetId
        {
            get
            {
                if (_stems == null) return null;
                var set = _stems.CurrentSet;
                return set != null ? set.ResolvedId : null; // Unity null check; ?. would bypass it
            }
        }

        public bool TryGetStemVolume(string stemId, out float linear01)
        {
            linear01 = 0f;
            return _stems != null && _stems.TryGetStemVolume(stemId, out linear01);
        }

        private float ResolveMusicFade(float fadeSeconds)
        {
            if (fadeSeconds >= 0f) return fadeSeconds;
            return _config != null ? _config.DefaultMusicFadeSeconds : 1.5f;
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
            CancelVoiceFade(voice);
            voice.Fading = true;
            _pool.StartCoroutine(FadeOutVoice(voice, index, fadeSeconds));
        }

        public void SetVoiceVolume(AudioHandle handle, float volume)
        {
            if (_pool.TryResolve(handle, out var voice, out _) && voice.Source != null)
            {
                CancelVoiceFade(voice);
                voice.Source.volume = Mathf.Max(0f, volume);
            }
        }

        /// <summary>
        /// Fades a pooled voice's volume to <paramref name="toVolume"/> over <paramref name="seconds"/>
        /// without stopping it; 0 or less snaps. A new fade replaces a running one. Safe no-op on stale handles.
        /// </summary>
        public void FadeVoice(AudioHandle handle, float toVolume, float seconds)
        {
            if (!_pool.TryResolve(handle, out var voice, out _) || voice.Source == null) return;
            CancelVoiceFade(voice);

            float target = Mathf.Max(0f, toVolume);
            if (seconds <= 0f)
            {
                voice.Source.volume = target;
                return;
            }

            // Deliberately not setting voice.Fading: the pool must still reclaim a one-shot that ends mid-fade.
            voice.FadeRoutine = _pool.StartCoroutine(FadeVoiceRoutine(voice, target, seconds));
        }

        private void CancelVoiceFade(AudioVoicePool.Voice voice)
        {
            if (voice.FadeRoutine == null) return;
            _pool.StopCoroutine(voice.FadeRoutine);
            voice.FadeRoutine = null;
        }

        private IEnumerator FadeVoiceRoutine(AudioVoicePool.Voice voice, float target, float seconds)
        {
            int generation = voice.Generation;
            float start = voice.Source.volume;
            float t = 0f;
            while (t < seconds)
            {
                // Bail if the voice was released or recycled out from under us.
                if (!voice.Active || voice.Generation != generation) yield break;
                t += Time.unscaledDeltaTime;
                voice.Source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            if (voice.Active && voice.Generation == generation)
            {
                voice.Source.volume = target;
                voice.FadeRoutine = null;
            }
        }

        public void SetVoicePitch(AudioHandle handle, float pitch)
        {
            if (_pool.TryResolve(handle, out var voice, out _) && voice.Source != null)
                voice.Source.pitch = pitch;
        }

        public void SetVoiceLoop(AudioHandle handle, bool loop)
        {
            if (_pool.TryResolve(handle, out var voice, out _) && voice.Source != null)
                voice.Source.loop = loop;
        }

        public void SetVoiceTime(AudioHandle handle, float seconds)
        {
            if (!_pool.TryResolve(handle, out var voice, out _) || voice.Source == null) return;
            var src = voice.Source;
            if (src.clip == null) return;
            // AudioSource.time throws if set to/after the clip length; leave a small margin.
            src.time = Mathf.Clamp(seconds, 0f, Mathf.Max(0f, src.clip.length - 0.01f));
        }

        public bool IsVoicePlaying(AudioHandle handle) => _pool.IsVoicePlaying(handle);

        public void AttachVoice(AudioHandle handle, Transform target)
        {
            if (!_pool.TryResolve(handle, out var voice, out _) || voice.Source == null) return;
            if (target != null && voice.Source.spatialBlend <= 0f && voice.Sound != null) WarnFollowOn2D(voice.Sound.Id);
            _pool.Attach(voice, target);
        }

        public void SetVoicePosition(AudioHandle handle, Vector3 position)
        {
            if (!_pool.TryResolve(handle, out var voice, out _) || voice.Source == null) return;
            _pool.Attach(voice, null); // otherwise the target's position would overwrite this next frame
            voice.Source.transform.position = position;
        }

        public void SetVoiceScheduledEnd(AudioHandle handle, double dspTime)
        {
            if (_pool.TryResolve(handle, out var voice, out _) && voice.Source != null)
                _pool.SetScheduledEnd(voice, dspTime);
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
            if (bus == AudioBus.Music)
            {
                StopMusic(0f);
                if (_stems != null) _stems.StopAll();
            }
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            _pool.SetPaused(paused);
            if (_musicA != null) { if (paused) _musicA.Pause(); else _musicA.UnPause(); }
            if (_musicB != null) { if (paused) _musicB.Pause(); else _musicB.UnPause(); }
            if (_stems != null) _stems.SetPaused(paused);
        }
    }
}
