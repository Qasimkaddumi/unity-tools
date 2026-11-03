using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using Kaddumi.UnityTools.Audio.Interfaces;
using Kaddumi.UnityTools.Audio.Providers;
using Kaddumi.UnityTools.Save;
using Kaddumi.UnityTools.Services;
using UnityEngine;
using UnityEngine.Audio;

namespace Kaddumi.UnityTools.Audio
{
    /// <summary>
    /// MonoBehaviour host and public API for the audio system. Registers the backend selected in
    /// the inspector, resolves string IDs to <see cref="SoundDefinition"/>s via the
    /// <see cref="SoundLibrary"/>, and forwards playback/volume calls to the active
    /// <see cref="IAudioProvider"/>. Initializes as an <see cref="IService"/> under the
    /// ServiceLocator, structurally identical to <c>SaveManager</c>/<c>AdManager</c>.
    ///
    /// <para>Per-bus volumes/mutes are the manager's source of truth and persist through the
    /// existing Save system via an <see cref="AudioSettingsSaveable"/>. Game code never touches
    /// <c>AudioSource</c> directly.</para>
    ///
    /// <para><b>Setup:</b> build an AudioMixer with a group per bus and expose each group's
    /// Volume param; fill those names into the <see cref="AudioConfig"/>. Create the config,
    /// a <see cref="SoundLibrary"/>, and a provider SO, then add this component under the
    /// ServiceLocator and assign all three.</para>
    /// </summary>
    public class AudioManager : MonoBehaviour, IService
    {
        public static AudioManager Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Shared audio configuration (mixer, buses, pool sizing, fade/duck defaults).")]
        [SerializeField] private AudioConfig config;

        [Tooltip("Catalog mapping string IDs to sounds. Required to play by ID.")]
        [SerializeField] private SoundLibrary library;

        [Tooltip("Audio backend ScriptableObject (UnityAudio by default).")]
        [SerializeField] private AudioProviderSO provider;

        [Tooltip("Persist bus volumes/mutes through the Save system. When off, config defaults are used each run.")]
        [SerializeField] private bool persistSettings = true;

        private IAudioProvider _provider;
        private AudioSettingsSaveable _saveable;

        // Manager is the source of truth for bus settings; mirrored into the provider.
        private readonly Dictionary<AudioBus, float> _volumes = new Dictionary<AudioBus, float>();
        private readonly Dictionary<AudioBus, bool> _muted = new Dictionary<AudioBus, bool>();

        /// <summary>Raised whenever a bus volume or mute state changes (e.g. to update UI sliders).</summary>
        public event Action<AudioBus> OnBusChanged;

        /// <summary>
        /// Raised when a voice reaches its natural end. Not raised when it is stopped or stolen, or when
        /// a looping voice stops because its target was destroyed; use <see cref="IsVoicePlaying"/> for those.
        /// </summary>
        public event Action<AudioHandle> VoiceFinished;

        /// <summary>
        /// Sets the configuration from code instead of the inspector. Call before <see cref="Initialize"/>.
        /// The library can be null when sounds are only played from assets.
        /// </summary>
        public void Configure(AudioConfig audioConfig, SoundLibrary soundLibrary, AudioProviderSO audioProvider)
        {
            if (_provider != null)
            {
                Debug.LogWarning("[AudioManager] Configure called after Initialize; ignoring it.");
                return;
            }
            config = audioConfig;
            library = soundLibrary;
            provider = audioProvider;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Action onComplete)
        {
            if (provider == null)
            {
                Debug.LogError("[AudioManager] No Audio Provider assigned. Assign an AudioProviderSO in the inspector.");
                onComplete?.Invoke();
                return;
            }

            SeedDefaultsFromConfig();

            _provider = provider.CreateProvider();
            _provider.VoiceFinished += RaiseVoiceFinished;
            _provider.Initialize(config, () =>
            {
                // Push seeded defaults into the freshly-created provider.
                foreach (var kv in _volumes) _provider.SetBusVolume(kv.Key, kv.Value);
                foreach (var kv in _muted) _provider.SetBusMuted(kv.Key, kv.Value);

                RegisterSaveable();

                Debug.Log("[AudioManager] Initialized");
                onComplete?.Invoke();
            });
        }

        private void SeedDefaultsFromConfig()
        {
            _volumes.Clear();
            _muted.Clear();
            if (config == null || config.Buses == null) return;
            foreach (var binding in config.Buses)
            {
                if (binding == null) continue;
                _volumes[binding.Bus] = binding.DefaultVolume;
                _muted[binding.Bus] = false;
            }
        }

        private void RegisterSaveable()
        {
            if (!persistSettings) return;
            if (SaveManager.Instance == null)
            {
                Debug.LogWarning("[AudioManager] persistSettings is on but no SaveManager is present. " +
                                 "Volumes will fall back to config defaults each run.");
                return;
            }
            _saveable = new AudioSettingsSaveable(CaptureSettings, ApplySettings);
            SaveManager.Instance.Register(_saveable);
        }

        private void OnDestroy()
        {
            if (_saveable != null) SaveManager.Instance?.Unregister(_saveable);
            if (_provider != null) _provider.VoiceFinished -= RaiseVoiceFinished;
        }

        private void RaiseVoiceFinished(AudioHandle handle) => VoiceFinished?.Invoke(handle);

        // --- SFX -------------------------------------------------------------

        /// <summary>Plays a 2D one-shot/looping SFX by ID. Returns a handle for later Stop, or Invalid if unknown.</summary>
        public AudioHandle PlaySfx2D(string id)
        {
            if (!TryResolve(id, out var def)) return AudioHandle.Invalid;
            return _provider.PlaySfx(def, Vector3.zero, spatial: false);
        }

        /// <summary>
        /// Plays an SFX by ID at a world position. The sound is spatialized only if its
        /// <see cref="SoundDefinition.SpatialBlend"/> is greater than 0; otherwise it plays 2D.
        /// </summary>
        public AudioHandle PlaySfx(string id, Vector3 position = default)
        {
            if (!TryResolve(id, out var def)) return AudioHandle.Invalid;
            return _provider.PlaySfx(def, position, spatial: true);
        }

        /// <summary>Plays a 2D SFX directly from a sound asset — no library or string ID needed.</summary>
        public AudioHandle PlaySfx2D(SoundDefinition sound)
            => IsReady(sound) ? _provider.PlaySfx(sound, Vector3.zero, spatial: false) : AudioHandle.Invalid;

        /// <summary>
        /// Plays a sound asset directly at a world position. Spatialized only if the asset's
        /// <see cref="SoundDefinition.SpatialBlend"/> is greater than 0; otherwise it plays 2D.
        /// </summary>
        public AudioHandle PlaySfx(SoundDefinition sound, Vector3 position = default)
            => IsReady(sound) ? _provider.PlaySfx(sound, position, spatial: true) : AudioHandle.Invalid;

        /// <summary>
        /// Plays a sound asset with per-play overrides. Returns an invalid handle if the sound has no
        /// clip or a voice limit refuses it. Spatial and Follow are separate; see <see cref="SoundPlayOptions"/>.
        /// </summary>
        public AudioHandle PlaySfxWithOptions(SoundDefinition sound, in SoundPlayOptions options)
            => IsReady(sound) ? _provider.PlaySfxWithOptions(sound, options) : AudioHandle.Invalid;

        /// <summary>
        /// Plays a sound asset starting at an exact DSP time (see <c>AudioSettings.dspTime</c>), for
        /// gapless start, loop and end clips. The voice counts as playing while it waits.
        /// </summary>
        public AudioHandle PlaySfxScheduled(SoundDefinition sound, in SoundPlayOptions options, double dspTime)
            => IsReady(sound) ? _provider.PlaySfxScheduled(sound, options, dspTime) : AudioHandle.Invalid;

        // --- Clip data -------------------------------------------------------

        /// <summary>
        /// Adds a reference to the sounds' clips and loads them. Pair it with <see cref="UnloadSounds"/>.
        /// <paramref name="done"/> always fires once. Only clips with "Load In Background" ticked in their
        /// import settings avoid blocking the main thread; others log a warning.
        /// </summary>
        public void LoadSounds(IReadOnlyList<SoundDefinition> sounds, Action<float> progress = null, Action done = null)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[AudioManager] Not initialized yet; ignoring audio request.");
                done?.Invoke();
                return;
            }
            _provider.LoadSounds(sounds, progress, done);
        }

        /// <summary>Drops the references added by <see cref="LoadSounds"/>. A clip unloads once nothing needs or plays it.</summary>
        public void UnloadSounds(IReadOnlyList<SoundDefinition> sounds) => _provider?.UnloadSounds(sounds);

        /// <summary>True when every clip of the sound is ready to play.</summary>
        public bool IsLoaded(SoundDefinition sound) => _provider != null && _provider.IsLoaded(sound);

        // --- Music -----------------------------------------------------------

        /// <summary>Crossfades to a music track by ID. Negative fade uses the config default.</summary>
        public void PlayMusic(string id, float fadeSeconds = -1f)
        {
            if (!TryResolve(id, out var def)) return;
            _provider.PlayMusic(def, fadeSeconds);
        }

        /// <summary>Crossfades to a music track directly from a sound asset. Negative fade uses the config default.</summary>
        public void PlayMusic(SoundDefinition sound, float fadeSeconds = -1f)
        {
            if (IsReady(sound)) _provider.PlayMusic(sound, fadeSeconds);
        }

        /// <summary>Crossfades to a music track at an explicit volume; null uses the sound's own. Negative fade uses the config default.</summary>
        public void PlayMusic(SoundDefinition sound, float fadeSeconds, float? volume)
        {
            if (IsReady(sound)) _provider.PlayMusic(sound, fadeSeconds, volume);
        }

        /// <summary>True from PlayMusic until the music is stopped, including while paused. Stems don't count; see <see cref="CurrentStemSetId"/>.</summary>
        public bool IsMusicPlaying => _provider != null && _provider.IsMusicPlaying;

        /// <summary>The sound started by PlayMusic, or null when none is.</summary>
        public SoundDefinition CurrentMusic => _provider?.CurrentMusic;

        /// <summary>Playback position of the current music in seconds; 0 when none is playing.</summary>
        public float MusicTime => _provider != null ? _provider.MusicTime : 0f;

        /// <summary>Length of the current music clip in seconds; 0 when none is playing.</summary>
        public float MusicLength => _provider != null ? _provider.MusicLength : 0f;

        /// <summary>
        /// How many times the current music has wrapped back to its start. It changes only after the
        /// track has restarted; to crossfade before the end, compare <see cref="MusicTime"/> to <see cref="MusicLength"/>.
        /// </summary>
        public int MusicLoopCount => _provider != null ? _provider.MusicLoopCount : 0;

        /// <summary>Fades out the current music. Negative fade uses the config default.</summary>
        public void StopMusic(float fadeSeconds = -1f) => _provider?.StopMusic(fadeSeconds);

        /// <summary>
        /// Crossfades to a layered music track (a <see cref="MusicStemSet"/>) by ID. All stems play in
        /// sync; use <see cref="SetStemVolume"/> to fade individual layers. Negative fade uses the config default.
        /// Calling it with the set that is already playing does nothing; call StopStems(0) first to restart it.
        /// </summary>
        public void PlayStems(string id, float fadeSeconds = -1f)
        {
            if (!TryResolveStemSet(id, out var set)) return;
            _provider.PlayStems(set, fadeSeconds);
        }

        /// <summary>
        /// Crossfades to a layered music track directly from a stem set asset. Negative fade uses the config default.
        /// Calling it with the set that is already playing does nothing; call StopStems(0) first to restart it.
        /// </summary>
        public void PlayStems(MusicStemSet set, float fadeSeconds = -1f)
        {
            if (IsReady(set)) _provider.PlayStems(set, fadeSeconds);
        }

        /// <summary>
        /// Fades one layer of the playing stem set to a linear volume (0..1) while all layers keep
        /// playing. Negative fade uses the config's default music fade; 0 snaps.
        /// </summary>
        public void SetStemVolume(string stemId, float linear01, float fadeSeconds = -1f)
            => _provider?.SetStemVolume(stemId, Mathf.Clamp01(linear01), fadeSeconds);

        /// <summary>Fades out and stops the current stem set. Negative fade uses the config default.</summary>
        public void StopStems(float fadeSeconds = -1f) => _provider?.StopStems(fadeSeconds);

        /// <summary>The ID of the stem set that is current (loading, playing, or fading in), or null when none is.</summary>
        public string CurrentStemSetId => _provider?.CurrentStemSetId;

        /// <summary>
        /// Reads the current level (0..1) of one stem of the current set, before the set's volume and
        /// any crossfade are applied. Returns false (and 0) when no stem set is current, the stem ID
        /// is unknown, or there is no provider.
        /// </summary>
        public bool TryGetStemVolume(string stemId, out float linear01)
        {
            linear01 = 0f;
            return _provider != null && _provider.TryGetStemVolume(stemId, out linear01);
        }

        // --- Bus volume ------------------------------------------------------

        /// <summary>Sets a bus volume (linear 0..1), applies it to the mixer, and raises <see cref="OnBusChanged"/>.</summary>
        public void SetBusVolume(AudioBus bus, float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            _volumes[bus] = linear01;
            _provider?.SetBusVolume(bus, linear01);
            OnBusChanged?.Invoke(bus);
        }

        /// <summary>Current linear volume of a bus (1 if never set).</summary>
        public float GetBusVolume(AudioBus bus) => _volumes.TryGetValue(bus, out var v) ? v : 1f;

        /// <summary>Mutes/unmutes a bus without losing its stored volume.</summary>
        public void SetBusMuted(AudioBus bus, bool muted)
        {
            _muted[bus] = muted;
            _provider?.SetBusMuted(bus, muted);
            OnBusChanged?.Invoke(bus);
        }

        /// <summary>True if the bus is currently muted.</summary>
        public bool IsBusMuted(AudioBus bus) => _muted.TryGetValue(bus, out var m) && m;

        /// <summary>Flips the mute state of a bus.</summary>
        public void ToggleMute(AudioBus bus) => SetBusMuted(bus, !IsBusMuted(bus));

        // --- Mixer routing ---------------------------------------------------

        /// <summary>
        /// The <see cref="AudioMixerGroup"/> a bus routes through, or null if the bus isn't
        /// configured (or no <see cref="AudioConfig"/>/mixer is assigned). Use this to route an
        /// external audio source — e.g. a <c>VideoPlayer</c>'s audio track — through the same bus
        /// as the rest of the game, so this bus's volume, mute, and ducking all apply to it too.
        /// Reads straight from the config, so it is valid even before the provider finishes
        /// initializing. See <see cref="Video.VideoAudioRouter"/>.
        /// </summary>
        public AudioMixerGroup GetBusGroup(AudioBus bus)
            => config != null ? config.GetBinding(bus)?.Group : null;

        // --- Ducking ---------------------------------------------------------

        /// <summary>Low-level duck: drops a bus to a target volume with explicit attack/release/hold.</summary>
        public void Duck(AudioBus bus, float toLinear01, float attackSeconds, float releaseSeconds, float holdSeconds = 0f)
            => _provider?.Duck(bus, toLinear01, attackSeconds, releaseSeconds, holdSeconds);

        /// <summary>
        /// Convenience duck of the configured DuckBus (typically Music) using the config's
        /// target/attack/release. Pass a positive <paramref name="holdSeconds"/> to auto-release
        /// after that time; otherwise the bus stays ducked until <see cref="Unduck"/>.
        /// </summary>
        public void DuckMusicForVoice(float holdSeconds = 0f)
        {
            if (config == null) return;
            _provider?.Duck(config.DuckBus, config.DuckTargetVolume,
                config.DuckAttackSeconds, config.DuckReleaseSeconds, holdSeconds);
        }

        /// <summary>Releases a held duck by ramping the bus back to its stored volume.</summary>
        public void Unduck(AudioBus bus, float releaseSeconds = -1f)
        {
            if (config == null) return;
            float release = releaseSeconds < 0f ? config.DuckReleaseSeconds : releaseSeconds;
            // Duck 'to' the stored volume with a tiny hold so the routine restores and clears itself.
            _provider?.Duck(bus, GetBusVolume(bus), release, 0f, 0.0001f);
        }

        /// <summary>
        /// Releases the held duck applied by <see cref="DuckMusicForVoice"/>, ramping the
        /// configured <see cref="AudioConfig.DuckBus"/> back to its stored volume. Symmetric
        /// partner to <see cref="DuckMusicForVoice"/> so callers need not know the duck bus.
        /// </summary>
        public void UnduckMusic(float releaseSeconds = -1f)
        {
            if (config == null) return;
            Unduck(config.DuckBus, releaseSeconds);
        }

        // --- Voice / lifecycle ----------------------------------------------

        /// <summary>Stops a specific SFX voice, optionally fading. Safe no-op on stale handles.</summary>
        public void Stop(AudioHandle handle, float fadeSeconds = 0f) => _provider?.Stop(handle, fadeSeconds);

        /// <summary>Sets a live voice's linear volume (per-source, on top of its bus). Safe no-op on stale handles.</summary>
        public void SetVoiceVolume(AudioHandle handle, float volume) => _provider?.SetVoiceVolume(handle, volume);

        /// <summary>Fades a live voice's volume to a target over time without stopping it. Safe no-op on stale handles.</summary>
        public void FadeVoice(AudioHandle handle, float toVolume, float seconds) => _provider?.FadeVoice(handle, toVolume, seconds);

        /// <summary>Sets a live voice's pitch / playback rate (1 = normal). Safe no-op on stale handles.</summary>
        public void SetVoicePitch(AudioHandle handle, float pitch) => _provider?.SetVoicePitch(handle, pitch);

        /// <summary>Toggles looping on a live voice. Safe no-op on stale handles.</summary>
        public void SetVoiceLoop(AudioHandle handle, bool loop) => _provider?.SetVoiceLoop(handle, loop);

        /// <summary>Seeks a live voice to a playback position in seconds (clamped to the clip). Safe no-op on stale handles.</summary>
        public void SetVoiceTime(AudioHandle handle, float seconds) => _provider?.SetVoiceTime(handle, seconds);

        /// <summary>
        /// Makes a voice's position track an object; null detaches it. If the target is destroyed, a
        /// looping voice stops and a one-shot finishes at its last position. Safe no-op on stale handles.
        /// </summary>
        public void AttachVoice(AudioHandle handle, Transform target) => _provider?.AttachVoice(handle, target);

        /// <summary>Moves a live voice, detaching it from any target. Safe no-op on stale handles.</summary>
        public void SetVoicePosition(AudioHandle handle, Vector3 position) => _provider?.SetVoicePosition(handle, position);

        /// <summary>
        /// True while a voice is playing or waiting to: paused, fading, loading, or scheduled but not
        /// started. False once it has ended or the handle is stale.
        /// </summary>
        public bool IsVoicePlaying(AudioHandle handle) => _provider != null && _provider.IsVoicePlaying(handle);

        /// <summary>Ends a voice at an exact DSP time, including a looping voice. Safe no-op on stale handles.</summary>
        public void SetVoiceScheduledEnd(AudioHandle handle, double dspTime) => _provider?.SetVoiceScheduledEnd(handle, dspTime);

        /// <summary>Stops every active voice on a bus.</summary>
        public void StopBus(AudioBus bus) => _provider?.StopBus(bus);

        /// <summary>Pauses all audio (e.g. when opening a pause menu).</summary>
        public void PauseAll() => _provider?.SetPaused(true);

        /// <summary>Resumes all audio previously paused with <see cref="PauseAll"/>.</summary>
        public void ResumeAll() => _provider?.SetPaused(false);

        // --- Save integration ------------------------------------------------

        private AudioSettingsState CaptureSettings()
        {
            var state = new AudioSettingsState();
            foreach (var kv in _volumes)
            {
                state.Buses.Add(new AudioSettingsState.BusSetting
                {
                    Bus = kv.Key,
                    Volume = kv.Value,
                    Muted = IsBusMuted(kv.Key),
                });
            }
            return state;
        }

        private void ApplySettings(AudioSettingsState state)
        {
            if (state?.Buses == null) return;
            foreach (var bus in state.Buses)
            {
                SetBusVolume(bus.Bus, bus.Volume);
                SetBusMuted(bus.Bus, bus.Muted);
            }
        }

        // --- Helpers ---------------------------------------------------------

        private bool IsReady(SoundDefinition sound)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[AudioManager] Not initialized yet; ignoring audio request.");
                return false;
            }
            if (sound == null)
            {
                Debug.LogWarning("[AudioManager] Null sound asset passed to an audio request.");
                return false;
            }
            return true;
        }

        private bool IsReady(MusicStemSet set)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[AudioManager] Not initialized yet; ignoring audio request.");
                return false;
            }
            if (set == null)
            {
                Debug.LogWarning("[AudioManager] Null music stem set passed to an audio request.");
                return false;
            }
            return true;
        }

        private bool TryResolveStemSet(string id, out MusicStemSet set)
        {
            set = null;
            if (_provider == null)
            {
                Debug.LogWarning("[AudioManager] Not initialized yet; ignoring audio request.");
                return false;
            }
            if (library == null)
            {
                Debug.LogWarning("[AudioManager] No SoundLibrary assigned; cannot play by ID.");
                return false;
            }
            if (!library.TryGetStemSet(id, out set))
            {
                Debug.LogWarning($"[AudioManager] Stem set '{id}' not found in the assigned SoundLibrary.");
                return false;
            }
            return true;
        }

        private bool TryResolve(string id, out SoundDefinition def)
        {
            def = null;
            if (_provider == null)
            {
                Debug.LogWarning("[AudioManager] Not initialized yet; ignoring audio request.");
                return false;
            }
            if (library == null)
            {
                Debug.LogWarning("[AudioManager] No SoundLibrary assigned; cannot play by ID.");
                return false;
            }
            if (!library.TryGet(id, out def))
            {
                Debug.LogWarning($"[AudioManager] Sound '{id}' not found in the assigned SoundLibrary.");
                return false;
            }
            return true;
        }

        [ContextMenu("Stop All Music")]
        private void TestStopMusic() => StopMusic();

        [ContextMenu("Pause All")]
        private void TestPause() => PauseAll();

        [ContextMenu("Resume All")]
        private void TestResume() => ResumeAll();
    }
}
