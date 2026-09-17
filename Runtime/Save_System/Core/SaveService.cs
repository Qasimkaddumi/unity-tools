using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Plain-C# domain service that is the SDK-agnostic heart of the save system.
    /// It owns the registry of <see cref="ISaveable"/> objects, the set of named
    /// <see cref="SaveStore"/>s, and orchestrates every slot operation.
    ///
    /// <para>Routing is per-saveable: each <see cref="ISaveable.StorageId"/> names the
    /// store its data belongs to, so a single <see cref="Save(int, Action{SaveReport})"/>
    /// captures the registry, groups it by store, and writes <em>one payload per store</em>
    /// — settings into a local file, progression into a cloud backend — then folds the
    /// per-store results into one <see cref="SaveReport"/>. Loads reverse the flow,
    /// restoring only the saveables currently routed to each store.</para>
    ///
    /// <para>The first registered store is the default: saveables that name no store (or
    /// name one that isn't bound) fall back to it. <c>SaveManager</c> is only the
    /// MonoBehaviour host that feeds this service its stores and config from the inspector.</para>
    /// </summary>
    public partial class SaveService
    {
        /// <summary>Which fan-out this is, so only saves and loads raise their events.</summary>
        private enum SaveOperation { Save, Load, Delete }

        private readonly Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();

        // Ordered: index 0 is the default store. Small enough that a list beats a dictionary,
        // and the order is exactly what the SaveManager inspector shows.
        private readonly List<SaveStore> stores = new List<SaveStore>();

        private ISaveSerializer serializer;

        /// <summary>Save-format version stamped into metadata and checked on load.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Optional supplier of accumulated play time, written into each save's metadata.</summary>
        public Func<double> PlaytimeProvider { get; set; }

        /// <summary>Raised after a slot is written successfully. Argument is the slot index.</summary>
        public event Action<int> OnSaved;

        /// <summary>Raised after a slot is loaded and applied successfully. Argument is the slot index.</summary>
        public event Action<int> OnLoaded;

        /// <summary>Raised when any save/load operation fails.</summary>
        public event Action<SaveError> OnError;

        public SaveService(ISaveSerializer serializer = null)
        {
            this.serializer = serializer ?? new JsonSaveSerializer();
        }

        public void SetSerializer(ISaveSerializer newSerializer) =>
            serializer = newSerializer ?? new JsonSaveSerializer();

        // --- Store registry ---------------------------------------------------

        /// <summary>Bound stores, in registration order. The first one is the default.</summary>
        public IReadOnlyList<SaveStore> Stores => stores;

        /// <summary>Id of the fallback store for saveables that name none, or null if nothing is bound.</summary>
        public string DefaultStoreId => stores.Count > 0 ? stores[0].Id : null;

        /// <summary>Binds a store, replacing any existing one with the same id (order is preserved).</summary>
        public void RegisterStore(SaveStore store)
        {
            if (store == null) return;

            int existing = IndexOfStore(store.Id);
            if (existing >= 0)
            {
                Debug.LogWarning($"[SaveService] Store '{store.Id}' is already registered; " +
                                 "the later registration replaces it. Store ids must be unique.");
                stores[existing] = store;
                return;
            }
            stores.Add(store);
        }

        public SaveStore RegisterStore(string id, ISaveProvider provider, bool required = true,
            bool includeInAutoSave = true, string mirrorOf = null)
        {
            var store = new SaveStore(id, provider, required, includeInAutoSave, mirrorOf);
            RegisterStore(store);
            return store;
        }

        public bool UnregisterStore(string id)
        {
            int index = IndexOfStore(id);
            if (index < 0) return false;
            stores.RemoveAt(index);
            return true;
        }

        public bool HasStore(string id) => IndexOfStore(id) >= 0;

        /// <summary>Returns the bound store with this id, or null.</summary>
        public SaveStore GetStore(string id)
        {
            int index = IndexOfStore(id);
            return index >= 0 ? stores[index] : null;
        }

        /// <summary>
        /// Stores that mirror <paramref name="storeId"/>. Usually none or one — a cloud store
        /// with its local cache — but nothing stops a store having several.
        /// </summary>
        public List<SaveStore> MirrorsOf(string storeId)
        {
            var mirrors = new List<SaveStore>();
            if (string.IsNullOrEmpty(storeId)) return mirrors;

            foreach (SaveStore store in stores)
            {
                if (store.IsMirror && string.Equals(store.MirrorOf, storeId, StringComparison.OrdinalIgnoreCase))
                {
                    mirrors.Add(store);
                }
            }
            return mirrors;
        }

        // --- Saveable registry ------------------------------------------------

        public void RegisterSaveable(ISaveable saveable)
        {
            if (saveable == null || string.IsNullOrEmpty(saveable.SaveKey)) return;

            if (saveables.TryGetValue(saveable.SaveKey, out var existing) && existing != saveable)
            {
                Debug.LogWarning($"[SaveService] Duplicate SaveKey '{saveable.SaveKey}'. " +
                                 "The later registration overwrites the earlier one; keys must be unique.");
            }
            saveables[saveable.SaveKey] = saveable;
        }

        public void UnregisterSaveable(ISaveable saveable)
        {
            if (saveable == null) return;
            if (saveables.TryGetValue(saveable.SaveKey, out var existing) && existing == saveable)
            {
                saveables.Remove(saveable.SaveKey);
            }
        }

        // --- Persistence ------------------------------------------------------

        /// <summary>Writes the slot to every bound store.</summary>
        public void Save(int slot, Action<SaveReport> onComplete = null) => Save(slot, null, onComplete);

        /// <summary>Writes the slot to one store only. Saveables routed elsewhere are untouched.</summary>
        public void SaveTo(int slot, string storeId, Action<SaveReport> onComplete = null) =>
            Save(slot, new[] { storeId }, onComplete);

        /// <summary>
        /// Writes the slot to the named stores (null targets them all). Every targeted store
        /// is written even when nothing currently routes to it, so each one carries the
        /// slot's <see cref="SaveMetadata"/> and a "load game" screen can list it.
        /// </summary>
        public void Save(int slot, IEnumerable<string> storeIds, Action<SaveReport> onComplete = null)
        {
            if (!TryResolveTargets(storeIds, includeMirrors: true, out List<SaveStore> targets, out SaveReport failure))
            {
                Report(onComplete, failure);
                return;
            }

            Dictionary<string, List<ISaveable>> routed = GroupSaveablesByStore();

            RunAcrossStores(targets, slot, SaveOperation.Save, onComplete, (store, done) =>
            {
                string payload;
                try
                {
                    routed.TryGetValue(store.Id, out List<ISaveable> owned);
                    payload = serializer.Serialize(Capture(slot, owned));
                }
                catch (Exception e)
                {
                    done(SaveStoreOutcome.Fail(store, SaveErrorType.Serialization,
                        $"Failed to capture state for store '{store.Id}': {e.Message}"));
                    return;
                }

                store.Provider.Write(SlotKey(slot), payload,
                    result => done(SaveStoreOutcome.From(store, result)));
            });
        }

        /// <summary>Reads the slot from every bound store and applies it.</summary>
        public void Load(int slot, Action<SaveReport> onComplete = null) => Load(slot, null, onComplete);

        /// <summary>Reads and applies the slot from one store only.</summary>
        public void LoadFrom(int slot, string storeId, Action<SaveReport> onComplete = null) =>
            Load(slot, new[] { storeId }, onComplete);

        /// <summary>
        /// Reads the named stores (null targets every non-mirror store) and restores each
        /// one's saveables. Only saveables currently routed to a store are restored from it,
        /// so re-routing a saveable leaves its old data behind rather than applying it twice.
        ///
        /// <para>When a store can't be read — an offline cloud backend, most often — its
        /// mirrors are tried in turn before the load is called a failure. That is what makes
        /// a mirrored cloud store playable on a plane.</para>
        /// </summary>
        public void Load(int slot, IEnumerable<string> storeIds, Action<SaveReport> onComplete = null)
        {
            if (!TryResolveTargets(storeIds, includeMirrors: false, out List<SaveStore> targets, out SaveReport failure))
            {
                Report(onComplete, failure);
                return;
            }

            Dictionary<string, List<ISaveable>> routed = GroupSaveablesByStore();

            RunAcrossStores(targets, slot, SaveOperation.Load, onComplete, (store, done) =>
            {
                // An explicitly named store is read on its own; an unfiltered load lets a
                // primary fall back to its mirrors.
                List<SaveStore> chain = storeIds == null ? MirrorsOf(store.Id) : null;
                LoadFromChain(slot, store, chain, 0, routed, done);
            });
        }

        /// <summary>
        /// Reads <paramref name="store"/> and, if that fails, each fallback in turn. The
        /// outcome reported is the first success, or — when every attempt fails — the
        /// primary's error, since that is the one that describes what the caller asked for.
        /// </summary>
        private void LoadFromChain(int slot, SaveStore store, List<SaveStore> fallbacks, int attempt,
            Dictionary<string, List<ISaveable>> routed, Action<SaveStoreOutcome> done)
        {
            SaveStore reading = attempt == 0 ? store : fallbacks[attempt - 1];

            reading.Provider.Read(SlotKey(slot), result =>
            {
                SaveStoreOutcome outcome = ApplyLoadedPayload(slot, store, reading, result, routed);
                if (outcome.Success)
                {
                    if (attempt > 0)
                    {
                        Debug.Log($"[SaveService] Store '{store.Id}' was unreachable for slot {slot}; " +
                                  $"loaded from its mirror '{reading.Id}' instead.");
                    }
                    done(outcome);
                    return;
                }

                bool haveFallbackLeft = fallbacks != null && attempt < fallbacks.Count;
                if (haveFallbackLeft)
                {
                    LoadFromChain(slot, store, fallbacks, attempt + 1, routed, done);
                    return;
                }

                done(outcome);
            });
        }

        /// <summary>
        /// Validates a payload read from <paramref name="reading"/> and restores the saveables
        /// routed to <paramref name="store"/> — the two differ when a mirror stood in for its
        /// primary, and the routing that matters is always the primary's.
        /// </summary>
        private SaveStoreOutcome ApplyLoadedPayload(int slot, SaveStore store, SaveStore reading,
            SaveResult result, Dictionary<string, List<ISaveable>> routed)
        {
            if (!result.Success) return SaveStoreOutcome.From(store, result);

            SaveData data = serializer.Deserialize(result.Data);
            if (data == null)
            {
                return SaveStoreOutcome.Fail(store, SaveErrorType.Corrupted,
                    $"Save in slot {slot} (store '{reading.Id}') could not be parsed.");
            }

            if (data.Metadata != null && data.Metadata.Version > Version)
            {
                return SaveStoreOutcome.Fail(store, SaveErrorType.VersionMismatch,
                    $"Save version {data.Metadata.Version} in store '{reading.Id}' is newer than " +
                    $"supported version {Version}.");
            }

            try
            {
                routed.TryGetValue(store.Id, out List<ISaveable> owned);
                Restore(data, owned);
            }
            catch (Exception e)
            {
                return SaveStoreOutcome.Fail(store, SaveErrorType.Serialization,
                    $"Failed to restore state from store '{reading.Id}': {e.Message}");
            }

            return SaveStoreOutcome.Ok(store);
        }

        /// <summary>Deletes the slot from every bound store.</summary>
        public void Delete(int slot, Action<SaveReport> onComplete = null) => Delete(slot, null, onComplete);

        /// <summary>Deletes the slot from one store only.</summary>
        public void DeleteFrom(int slot, string storeId, Action<SaveReport> onComplete = null) =>
            Delete(slot, new[] { storeId }, onComplete);

        /// <summary>
        /// Deletes the slot from the named stores (null targets them all). A store that never
        /// held the slot counts as deleted — the caller's goal is "it's gone" either way.
        /// </summary>
        public void Delete(int slot, IEnumerable<string> storeIds, Action<SaveReport> onComplete = null)
        {
            if (!TryResolveTargets(storeIds, includeMirrors: true, out List<SaveStore> targets, out SaveReport failure))
            {
                Report(onComplete, failure);
                return;
            }

            RunAcrossStores(targets, slot, SaveOperation.Delete, onComplete, (store, done) =>
            {
                store.Provider.Delete(SlotKey(slot), result =>
                {
                    bool alreadyGone = !result.Success && result.Error.Type == SaveErrorType.NotFound;
                    SaveStoreOutcome outcome = alreadyGone
                        ? SaveStoreOutcome.Ok(store)
                        : SaveStoreOutcome.From(store, result);

                    // Drop the sync token as well, or a slot that is deleted and started
                    // over would be reconciled against an agreement that no longer exists.
                    store.Provider.Delete(SyncTokenKey(slot), _ =>
                    {
                        // Sweep the slot's blobs too, so deleting a save can't strand its
                        // screenshots. Their failures are logged, not fatal (see DeleteBlobsOf).
                        DeleteBlobsOf(slot, store, () => done(outcome));
                    });
                });
            });
        }

        /// <summary>True if <em>any</em> bound store holds this slot.</summary>
        public void Exists(int slot, Action<bool> onComplete)
        {
            if (stores.Count == 0)
            {
                onComplete?.Invoke(false);
                return;
            }

            int remaining = stores.Count;
            bool found = false;
            foreach (SaveStore store in stores)
            {
                store.Provider.Exists(SlotKey(slot), exists =>
                {
                    found |= exists;
                    if (--remaining == 0) onComplete?.Invoke(found);
                });
            }
        }

        /// <summary>True if the named store holds this slot.</summary>
        public void Exists(int slot, string storeId, Action<bool> onComplete)
        {
            SaveStore store = GetStore(storeId);
            if (store == null)
            {
                onComplete?.Invoke(false);
                return;
            }
            store.Provider.Exists(SlotKey(slot), onComplete);
        }

        /// <summary>
        /// Reads a slot's metadata from the default store without applying it, for
        /// "load game" listings. Every store stamps the same metadata on write, so the
        /// default store's copy is the one to list; pass a store id to read another's.
        /// </summary>
        public void GetMetadata(int slot, Action<SaveMetadata> onComplete) =>
            GetMetadata(slot, DefaultStoreId, onComplete);

        public void GetMetadata(int slot, string storeId, Action<SaveMetadata> onComplete)
        {
            SaveStore store = GetStore(storeId);
            if (store == null)
            {
                onComplete?.Invoke(null);
                return;
            }

            store.Provider.Read(SlotKey(slot), result =>
            {
                if (!result.Success)
                {
                    onComplete?.Invoke(null);
                    return;
                }
                SaveData data = serializer.Deserialize(result.Data);
                onComplete?.Invoke(data?.Metadata);
            });
        }

        // --- Internals --------------------------------------------------------

        /// <summary>
        /// Runs <paramref name="operation"/> against every target and completes once they have
        /// all reported. Providers may call back synchronously (file, PlayerPrefs) or
        /// asynchronously (cloud); the countdown only reaches zero after the last one reports,
        /// so both work without a special case.
        /// </summary>
        private void RunAcrossStores(List<SaveStore> targets, int slot, SaveOperation kind,
            Action<SaveReport> onComplete, Action<SaveStore, Action<SaveStoreOutcome>> operation)
        {
            var outcomes = new SaveStoreOutcome[targets.Count];
            int remaining = targets.Count;

            for (int i = 0; i < targets.Count; i++)
            {
                int index = i;
                bool reported = false;

                operation(targets[i], outcome =>
                {
                    // Guard against a provider invoking its callback twice, which would drive
                    // the countdown past zero and complete the report early (or never).
                    if (reported) return;
                    reported = true;

                    outcomes[index] = outcome;
                    if (--remaining > 0) return;

                    SaveReport report = SaveReport.From(outcomes);
                    if (report.Success)
                    {
                        if (kind == SaveOperation.Save) OnSaved?.Invoke(slot);
                        else if (kind == SaveOperation.Load) OnLoaded?.Invoke(slot);
                    }
                    Report(onComplete, report);
                });
            }
        }

        /// <summary>
        /// Resolves the requested store ids to bound stores; a null id list means "all".
        /// Fails when nothing is bound at all, or when a named store doesn't exist — a
        /// missing store is a wiring mistake, not something to silently skip.
        /// </summary>
        /// <param name="includeMirrors">
        /// Whether an unfiltered operation covers mirror stores. True for writes and deletes,
        /// which must keep a mirror in step; false for loads, where the primary is
        /// authoritative and its mirrors are only consulted as a fallback. A store named
        /// explicitly is always honoured, mirror or not.
        /// </param>
        private bool TryResolveTargets(IEnumerable<string> storeIds, bool includeMirrors,
            out List<SaveStore> targets, out SaveReport failure)
        {
            failure = null;

            if (stores.Count == 0)
            {
                targets = null;
                failure = SaveReport.Fail(SaveErrorType.ProviderNotAvailable,
                    "No save store is registered. Add one in the SaveManager inspector.");
                return false;
            }

            if (storeIds == null)
            {
                if (includeMirrors)
                {
                    targets = stores;
                    return true;
                }

                targets = new List<SaveStore>();
                foreach (SaveStore store in stores)
                {
                    if (!store.IsMirror) targets.Add(store);
                }

                if (targets.Count == 0)
                {
                    failure = SaveReport.Fail(SaveErrorType.ProviderNotAvailable,
                        "Every registered store is a mirror, so there is no primary to read from. " +
                        "Clear 'Mirror Of' on at least one store.");
                    return false;
                }
                return true;
            }

            targets = new List<SaveStore>();
            foreach (string id in storeIds)
            {
                SaveStore store = GetStore(id);
                if (store == null)
                {
                    targets = null;
                    failure = SaveReport.Fail(SaveErrorType.StoreNotFound,
                        $"No save store is registered with id '{id}'.");
                    return false;
                }
                if (!targets.Contains(store)) targets.Add(store);
            }

            if (targets.Count == 0)
            {
                failure = SaveReport.Fail(SaveErrorType.StoreNotFound,
                    "No save stores were targeted by this operation.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Buckets the registered saveables by the store they name. An unknown or empty
        /// <see cref="ISaveable.StorageId"/> falls back to the default store — dropping the
        /// data would be worse than putting it somewhere predictable — with a warning, since
        /// it always means a typo or a missing binding.
        /// </summary>
        private Dictionary<string, List<ISaveable>> GroupSaveablesByStore()
        {
            var routed = new Dictionary<string, List<ISaveable>>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in saveables)
            {
                string requested = pair.Value.StorageId;
                string resolved;

                if (string.IsNullOrWhiteSpace(requested))
                {
                    resolved = DefaultStoreId;
                }
                else
                {
                    SaveStore store = GetStore(requested);
                    if (store == null)
                    {
                        resolved = DefaultStoreId;
                        Debug.LogWarning($"[SaveService] Saveable '{pair.Key}' wants store '{requested}', " +
                                         $"which is not registered. Falling back to '{resolved}'. " +
                                         "Bind that store in the SaveManager inspector.");
                    }
                    else if (store.IsMirror)
                    {
                        // Routing at a mirror would be overwritten by its primary's bucket
                        // below, silently dropping this saveable. Point it at the primary,
                        // which is what the author meant; the mirror still gets the copy.
                        resolved = store.MirrorOf;
                        Debug.LogWarning($"[SaveService] Saveable '{pair.Key}' targets store '{requested}', " +
                                         $"which is a mirror of '{resolved}'. Routing it to '{resolved}' " +
                                         "instead — the mirror receives a copy either way.");
                    }
                    else
                    {
                        resolved = store.Id;
                    }
                }

                if (!routed.TryGetValue(resolved, out List<ISaveable> bucket))
                {
                    bucket = new List<ISaveable>();
                    routed[resolved] = bucket;
                }
                bucket.Add(pair.Value);
            }

            // A mirror carries a copy of its primary's data, so it inherits the same bucket.
            // Done after the main loop so it doesn't matter which order the stores were bound in.
            foreach (SaveStore store in stores)
            {
                if (!store.IsMirror) continue;

                if (routed.TryGetValue(store.MirrorOf, out List<ISaveable> primaryBucket))
                {
                    routed[store.Id] = primaryBucket;
                }
                else
                {
                    routed.Remove(store.Id);
                }
            }
            return routed;
        }

        private SaveData Capture(int slot, List<ISaveable> owned)
        {
            double playtime = PlaytimeProvider != null ? PlaytimeProvider() : 0d;
            var data = new SaveData { Metadata = SaveMetadata.Create(slot, Version, playtime) };

            if (owned == null) return data;
            foreach (ISaveable saveable in owned)
            {
                data.Set(saveable.SaveKey, saveable.CaptureState());
            }
            return data;
        }

        private static void Restore(SaveData data, List<ISaveable> owned)
        {
            if (owned == null) return;
            foreach (ISaveable saveable in owned)
            {
                string json = data.Get(saveable.SaveKey);
                if (json != null) saveable.RestoreState(json);
            }
        }

        private int IndexOfStore(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < stores.Count; i++)
            {
                if (string.Equals(stores[i].Id, id, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private void Report(Action<SaveReport> onComplete, SaveReport report)
        {
            if (!report.Success) OnError?.Invoke(report.Error);
            onComplete?.Invoke(report);
        }

        /// <summary>Stable storage key for a slot index, shared by every provider.</summary>
        public static string SlotKey(int slot) => $"slot_{slot}";
    }
}
