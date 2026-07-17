using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using Kaddumi.UnityTools.Audio.Interfaces;
using Kaddumi.UnityTools.Audio.Providers;
using Kaddumi.UnityTools.Save;
using Kaddumi.UnityTools.Services;
using UnityEngine;

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
        }

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

        /// <summary>Fades out the current music. Negative fade uses the config default.</summary>
        public void StopMusic(float fadeSeconds = -1f) => _provider?.StopMusic(fadeSeconds);

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

        // --- Voice / lifecycle ----------------------------------------------

        /// <summary>Stops a specific SFX voice, optionally fading. Safe no-op on stale handles.</summary>
        public void Stop(AudioHandle handle, float fadeSeconds = 0f) => _provider?.Stop(handle, fadeSeconds);

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
