using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Data;
using Kaddumi.UnityTools.Save.Interfaces;
using Kaddumi.UnityTools.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save
{
    /// <summary>
    /// MonoBehaviour host for the save system. Binds the storage backends listed in the
    /// inspector to named <see cref="SaveStore"/>s, owns a <see cref="SaveService"/>, and
    /// exposes a small, SDK-agnostic API for game code. Structurally identical to
    /// <c>AuthManager</c>: it initializes as an <see cref="IService"/> under the ServiceLocator.
    ///
    /// <para>A game normally binds two stores — a local file for settings and device-bound
    /// state, and a cloud backend for progression that follows the player — and each
    /// <see cref="ISaveable"/> picks one through <see cref="ISaveable.StorageId"/>. One
    /// <see cref="Save()"/> writes both and reports per-store results in a
    /// <see cref="SaveReport"/>.</para>
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

        [Header("Stores")]
        [Tooltip("Named storage targets. Each ISaveable routes to one of these ids via its " +
                 "StorageId, so local and cloud data can live side by side. The first row is the " +
                 "default for saveables that name no store.")]
        [SerializeField]
        private List<SaveStoreBinding> stores = new List<SaveStoreBinding>
        {
            new SaveStoreBinding { Id = SaveStores.Local }
        };

        /// <summary>Slot targeted by the parameterless <see cref="Save()"/>/<see cref="Load()"/> helpers.</summary>
        public int ActiveSlot { get; private set; }

        /// <summary>Raised after a slot is written successfully to every required store.</summary>
        public event Action<int> OnSaved;

        /// <summary>Raised after a slot is loaded and applied successfully.</summary>
        public event Action<int> OnLoaded;

        /// <summary>Raised when any save/load operation fails.</summary>
        public event Action<SaveError> OnError;

        // Saveables that registered before Initialize created the Service.
        private readonly List<ISaveable> _pending = new List<ISaveable>();

        // Ids of the stores the periodic auto-save writes; null when every store qualifies,
        // which lets the service take its "all stores" fast path.
        private string[] _autoSaveStoreIds;

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
            Service = new SaveService(new JsonSaveSerializer(config != null && config.PrettyPrint));
            Service.Version = config != null ? config.SaveVersion : 1;
            Service.PlaytimeProvider = () => _playtimeSeconds;

            Service.OnSaved += slot => OnSaved?.Invoke(slot);
            Service.OnLoaded += slot => OnLoaded?.Invoke(slot);
            Service.OnError += error => OnError?.Invoke(error);

            ActiveSlot = config != null ? config.DefaultSlot : 0;

            // Flush any saveables that registered during scene load, before we existed.
            foreach (ISaveable saveable in _pending) Service.RegisterSaveable(saveable);
            _pending.Clear();

            List<SaveStore> created = BindStores();
            if (created.Count == 0)
            {
                Debug.LogWarning("[SaveManager] No usable save store is configured in the inspector. " +
                                 "Saving/loading will fail until one is set.");
                onComplete?.Invoke();
                return;
            }

            CacheAutoSaveStores();

            // Providers may initialize asynchronously (cloud) or synchronously (file,
            // PlayerPrefs); wait for all of them before reporting the service as ready.
            int remaining = created.Count;
            foreach (SaveStore store in created)
            {
                store.Provider.Initialize(() =>
                {
                    if (--remaining > 0) return;

                    StartAutoSaveIfEnabled();
                    Debug.Log($"[SaveManager] Initialized with {created.Count} store(s): " +
                              $"{string.Join(", ", created)}");
                    onComplete?.Invoke();
                });
            }
        }

        /// <summary>
        /// Turns the inspector rows into runtime stores, skipping rows that are incomplete
        /// rather than failing initialization outright — a half-configured cloud row
        /// shouldn't take the local store down with it.
        /// </summary>
        private List<SaveStore> BindStores()
        {
            var created = new List<SaveStore>();
            if (stores == null) return created;

            for (int i = 0; i < stores.Count; i++)
            {
                SaveStoreBinding binding = stores[i];
                if (binding == null) continue;

                if (string.IsNullOrWhiteSpace(binding.Id))
                {
                    Debug.LogWarning($"[SaveManager] Store #{i} has no id and was skipped. " +
                                     "Give it an id saveables can route to (e.g. 'local').");
                    continue;
                }
                if (binding.Provider == null)
                {
                    Debug.LogWarning($"[SaveManager] Store '{binding.Id}' has no provider assigned " +
                                     "and was skipped.");
                    continue;
                }
                if (Service.HasStore(binding.Id))
                {
                    Debug.LogWarning($"[SaveManager] Duplicate store id '{binding.Id}'; only the first " +
                                     "row with that id is used.");
                    continue;
                }

                created.Add(Service.RegisterStore(binding.Id, binding.Provider.CreateProvider(),
                    binding.Required, binding.IncludeInAutoSave));
            }
            return created;
        }

        private void CacheAutoSaveStores()
        {
            var included = new List<string>();
            foreach (SaveStore store in Service.Stores)
            {
                if (store.IncludeInAutoSave) included.Add(store.Id);
            }

            // null means "all stores" to the service; only narrow the target when some
            // store actually opted out.
            _autoSaveStoreIds = included.Count == Service.Stores.Count ? null : included.ToArray();
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

        // --- Stores -----------------------------------------------------------

        /// <summary>The bound stores, in inspector order. The first one is the default.</summary>
        public IReadOnlyList<SaveStore> Stores =>
            Service != null ? Service.Stores : Array.Empty<SaveStore>();

        /// <summary>True if a store with this id is bound and ready to route to.</summary>
        public bool HasStore(string storeId) => Service != null && Service.HasStore(storeId);

        // --- Public API -------------------------------------------------------

        /// <summary>Saves the <see cref="ActiveSlot"/> to every bound store.</summary>
        public void Save(Action<SaveReport> onComplete = null) => Save(ActiveSlot, onComplete);

        public void Save(int slot, Action<SaveReport> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.Save(slot, WrapLog(onComplete));
        }

        /// <summary>
        /// Saves only the data routed to one store — e.g. push progression to the cloud
        /// after a level ends without rewriting local settings.
        /// </summary>
        public void SaveTo(string storeId, Action<SaveReport> onComplete = null) =>
            SaveTo(ActiveSlot, storeId, onComplete);

        public void SaveTo(int slot, string storeId, Action<SaveReport> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.SaveTo(slot, storeId, WrapLog(onComplete));
        }

        /// <summary>Loads and applies the <see cref="ActiveSlot"/> from every bound store.</summary>
        public void Load(Action<SaveReport> onComplete = null) => Load(ActiveSlot, onComplete);

        public void Load(int slot, Action<SaveReport> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.Load(slot, WrapLog(onComplete));
        }

        /// <summary>Loads and applies only the data routed to one store.</summary>
        public void LoadFrom(string storeId, Action<SaveReport> onComplete = null) =>
            LoadFrom(ActiveSlot, storeId, onComplete);

        public void LoadFrom(int slot, string storeId, Action<SaveReport> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.LoadFrom(slot, storeId, WrapLog(onComplete));
        }

        /// <summary>Deletes the slot from every bound store.</summary>
        public void Delete(int slot, Action<SaveReport> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.Delete(slot, WrapLog(onComplete));
        }

        /// <summary>Deletes the slot from one store only.</summary>
        public void DeleteFrom(int slot, string storeId, Action<SaveReport> onComplete = null)
        {
            if (!ValidateSlot(slot, onComplete)) return;
            Service.DeleteFrom(slot, storeId, WrapLog(onComplete));
        }

        /// <summary>True if any bound store holds this slot.</summary>
        public void HasSave(int slot, Action<bool> onComplete) => Service?.Exists(slot, onComplete);

        /// <summary>True if the named store holds this slot.</summary>
        public void HasSave(int slot, string storeId, Action<bool> onComplete) =>
            Service?.Exists(slot, storeId, onComplete);

        /// <summary>Reads a slot's metadata for a "load game" screen without applying it.</summary>
        public void GetMetadata(int slot, Action<SaveMetadata> onComplete) =>
            Service?.GetMetadata(slot, onComplete);

        /// <summary>Reads a slot's metadata from one store, e.g. to compare local against cloud.</summary>
        public void GetMetadata(int slot, string storeId, Action<SaveMetadata> onComplete) =>
            Service?.GetMetadata(slot, storeId, onComplete);

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
                AutoSave();
            }
        }

        /// <summary>Writes the default slot to the stores that opted into auto-saving.</summary>
        private void AutoSave()
        {
            int slot = config.DefaultSlot;
            if (!ValidateSlot(slot, null)) return;
            Service.Save(slot, _autoSaveStoreIds, WrapLog(null));
        }

        private void OnApplicationPause(bool paused)
        {
            // Backgrounding may be the last moment before the OS kills the app, so this
            // writes every store, not just the auto-save subset.
            if (paused && config != null && config.SaveOnPause && Service != null)
            {
                Save(config.DefaultSlot);
            }
        }

        private void OnApplicationQuit()
        {
            // Best effort across every store, but note that a remote store's request very
            // likely won't finish before the process goes away — treat OnApplicationPause
            // (which fires first on mobile, with the app still alive) as the reliable one,
            // and push to the cloud at natural checkpoints rather than relying on quit.
            if (config != null && config.SaveOnQuit && Service != null)
            {
                Save(config.DefaultSlot);
            }
        }

        // --- Helpers ----------------------------------------------------------

        private bool ValidateSlot(int slot, Action<SaveReport> onComplete)
        {
            if (Service == null)
            {
                onComplete?.Invoke(SaveReport.Fail(SaveErrorType.NotInitialized,
                    "SaveManager is not initialized yet."));
                return false;
            }
            if (config != null && (slot < 0 || slot >= config.SlotCount))
            {
                onComplete?.Invoke(SaveReport.Fail(SaveErrorType.InvalidSlot,
                    $"Slot {slot} is outside the configured range 0..{config.SlotCount - 1}."));
                return false;
            }
            return true;
        }

        private Action<SaveReport> WrapLog(Action<SaveReport> inner)
        {
            return report =>
            {
                if (!report.Success)
                {
                    Debug.LogWarning($"[SaveManager] Save operation failed: {report}");
                }
                else if (report.HasFailures)
                {
                    // Every required store made it, but an optional one (typically cloud)
                    // didn't — worth surfacing without treating the save as lost.
                    Debug.Log($"[SaveManager] Save operation succeeded with optional store " +
                              $"failures: {report}");
                }
                inner?.Invoke(report);
            };
        }

        [ContextMenu("Save (Active Slot)")]
        private void TestSave() =>
            Save(report => Debug.Log($"[SaveManager] Save {report}"));

        [ContextMenu("Load (Active Slot)")]
        private void TestLoad() =>
            Load(report => Debug.Log($"[SaveManager] Load {report}"));
    }
}
