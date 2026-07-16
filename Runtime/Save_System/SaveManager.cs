using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Data;
using Kaddumi.UnityTools.Save.Interfaces;
using Kaddumi.UnityTools.Save.Providers;
using Kaddumi.UnityTools.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save
{
    /// <summary>
    /// MonoBehaviour host for the save system. Registers the storage provider selected in
    /// the inspector, owns a <see cref="SaveService"/>, and exposes a small, SDK-agnostic
    /// API for game code. Structurally identical to <c>AuthManager</c>: it initializes as
    /// an <see cref="IService"/> under the ServiceLocator.
    ///
    /// Also drives the lifecycle behaviour configured in <see cref="SaveConfig"/>: periodic
    /// auto-save, save-on-pause (mobile), save-on-quit, and play-time tracking.
    /// </summary>
    public class SaveManager : MonoBehaviour, IService
    {
        public static SaveManager Instance { get; private set; }

        public SaveService Service { get; private set; }

        [Header("Configuration")]
        [Tooltip("Shared save configuration (slots, auto-save, versioning). Optional but recommended.")]
        [SerializeField] private SaveConfig config;

        [Tooltip("Storage backend ScriptableObject (PlayerPrefs, File, or Encrypted File).")]
        [SerializeField] private SaveProviderSO provider;

        /// <summary>Slot targeted by the parameterless <see cref="Save()"/>/<see cref="Load()"/> helpers.</summary>
        public int ActiveSlot { get; private set; }

        /// <summary>Raised after a slot is written successfully.</summary>
        public event Action<int> OnSaved;

        /// <summary>Raised after a slot is loaded and applied successfully.</summary>
        public event Action<int> OnLoaded;

        /// <summary>Raised when any save/load operation fails.</summary>
        public event Action<SaveError> OnError;

        // Saveables that registered before Initialize created the Service.
        private readonly List<ISaveable> _pending = new List<ISaveable>();

        private double _playtimeSeconds;
        private Coroutine _autoSaveRoutine;

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
            Service = new SaveService(serializer: new JsonSaveSerializer(config != null && config.PrettyPrint));
            Service.Version = config != null ? config.SaveVersion : 1;
            Service.PlaytimeProvider = () => _playtimeSeconds;

            Service.OnSaved += slot => OnSaved?.Invoke(slot);
            Service.OnLoaded += slot => OnLoaded?.Invoke(slot);
            Service.OnError += error => OnError?.Invoke(error);

            ActiveSlot = config != null ? config.DefaultSlot : 0;

            // Flush any saveables that registered during scene load, before we existed.
            foreach (var saveable in _pending) Service.RegisterSaveable(saveable);
            _pending.Clear();

            if (provider == null)
            {
                Debug.LogWarning("[SaveManager] No save provider assigned in the inspector. " +
                                 "Saving/loading will fail until one is set.");
                onComplete?.Invoke();
                return;
            }

            var runtimeProvider = provider.CreateProvider();
            Service.SetProvider(runtimeProvider);
            runtimeProvider.Initialize(() =>
            {
                StartAutoSaveIfEnabled();
                Debug.Log("[SaveManager] Initialized");
                onComplete?.Invoke();
            });
        }

        private void Update()
        {
            if (config != null && config.TrackPlaytime)
            {
                _playtimeSeconds += Time.unscaledDeltaTime;
            }
        }

        // --- Saveable registration -------------------------------------------

        /// <summary>Registers a saveable so its state is included in future saves.</summary>
        public void Register(ISaveable saveable)
        {
            if (Service != null) Service.RegisterSaveable(saveable);
            else if (saveable != null && !_pending.Contains(saveable)) _pending.Add(saveable);
        }

        public void Unregister(ISaveable saveable)
        {
            if (Service != null) Service.UnregisterSaveable(saveable);
            else _pending.Remove(saveable);
        }

        // --- Public API -------------------------------------------------------

        /// <summary>Saves the <see cref="ActiveSlot"/>.</summary>
        public void Save(Action<SaveResult> onComplete = null) => Save(ActiveSlot, onComplete);

        public void Save(int slot, Action<SaveResult> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.Save(slot, WrapLog(onComplete));
        }

        /// <summary>Loads and applies the <see cref="ActiveSlot"/>.</summary>
        public void Load(Action<SaveResult> onComplete = null) => Load(ActiveSlot, onComplete);

        public void Load(int slot, Action<SaveResult> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.Load(slot, WrapLog(onComplete));
        }

        public void Delete(int slot, Action<SaveResult> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.Delete(slot, WrapLog(onComplete));
        }

        public void HasSave(int slot, Action<bool> onComplete) => Service?.Exists(slot, onComplete);

        /// <summary>Reads a slot's metadata for a "load game" screen without applying it.</summary>
        public void GetMetadata(int slot, Action<SaveMetadata> onComplete) =>
            Service?.GetMetadata(slot, onComplete);

        /// <summary>Sets the slot targeted by the parameterless <see cref="Save()"/>/<see cref="Load()"/>.</summary>
        public void SetActiveSlot(int slot)
        {
            if (config != null && (slot < 0 || slot >= config.SlotCount))
            {
                Debug.LogWarning($"[SaveManager] Slot {slot} is outside the configured range 0..{config.SlotCount - 1}.");
                return;
            }
            ActiveSlot = slot;
        }

        // --- Lifecycle --------------------------------------------------------

        private void StartAutoSaveIfEnabled()
        {
            if (config == null || !config.AutoSave) return;
            if (_autoSaveRoutine != null) StopCoroutine(_autoSaveRoutine);
            _autoSaveRoutine = StartCoroutine(AutoSaveLoop());
        }

        private IEnumerator AutoSaveLoop()
        {
            var wait = new WaitForSecondsRealtime(config.AutoSaveIntervalSeconds);
            while (true)
            {
                yield return wait;
                Save(config.DefaultSlot);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && config != null && config.SaveOnPause && Service != null)
            {
                Save(config.DefaultSlot);
            }
        }

        private void OnApplicationQuit()
        {
            if (config != null && config.SaveOnQuit && Service != null)
            {
                Save(config.DefaultSlot);
            }
        }

        // --- Helpers ----------------------------------------------------------

        private bool ValidateSlot(int slot, Action<SaveResult> onComplete)
        {
            if (Service == null)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.NotInitialized,
                    "SaveManager is not initialized yet."));
                return false;
            }
            if (config != null && (slot < 0 || slot >= config.SlotCount))
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.InvalidSlot,
                    $"Slot {slot} is outside the configured range 0..{config.SlotCount - 1}."));
                return false;
            }
            return true;
        }

        private Action<SaveResult> WrapLog(Action<SaveResult> inner)
        {
            return result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"[SaveManager] Save operation failed: {result.Error}");
                }
                inner?.Invoke(result);
            };
        }

        [ContextMenu("Save (Active Slot)")]
        private void TestSave() =>
            Save(result => Debug.Log($"[SaveManager] Save success={result.Success}"));

        [ContextMenu("Load (Active Slot)")]
        private void TestLoad() =>
            Load(result => Debug.Log($"[SaveManager] Load success={result.Success}"));
    }
}
