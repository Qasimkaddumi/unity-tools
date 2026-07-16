using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Plain-C# domain service that is the SDK-agnostic heart of the save system.
    /// It owns the registry of <see cref="ISaveable"/> objects and orchestrates every
    /// slot operation: on save it captures each saveable into a <see cref="SaveData"/>
    /// container, serializes it, and hands the payload to the active
    /// <see cref="ISaveProvider"/>; on load it reverses the flow. <c>SaveManager</c> is
    /// only the MonoBehaviour host that feeds it a provider and config from the inspector.
    /// </summary>
    public class SaveService
    {
        private readonly Dictionary<string, ISaveable> saveables = new Dictionary<string, ISaveable>();

        private ISaveProvider provider;
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

        public SaveService(ISaveProvider provider = null, ISaveSerializer serializer = null)
        {
            this.provider = provider;
            this.serializer = serializer ?? new JsonSaveSerializer();
        }

        public void SetProvider(ISaveProvider newProvider) => provider = newProvider;

        public void SetSerializer(ISaveSerializer newSerializer) =>
            serializer = newSerializer ?? new JsonSaveSerializer();

        // --- Saveable registry -----------------------------------------------

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

        public void Save(int slot, Action<SaveResult> onComplete = null)
        {
            if (!EnsureProvider(onComplete)) return;

            SaveData data;
            try
            {
                data = CaptureAll(slot);
            }
            catch (Exception e)
            {
                Fail(onComplete, SaveErrorType.Serialization, $"Failed to capture state: {e.Message}");
                return;
            }

            string payload = serializer.Serialize(data);
            provider.Write(SlotKey(slot), payload, result =>
            {
                if (result.Success)
                {
                    OnSaved?.Invoke(slot);
                }
                else
                {
                    OnError?.Invoke(result.Error);
                }
                onComplete?.Invoke(result);
            });
        }

        public void Load(int slot, Action<SaveResult> onComplete = null)
        {
            if (!EnsureProvider(onComplete)) return;

            provider.Read(SlotKey(slot), result =>
            {
                if (!result.Success)
                {
                    OnError?.Invoke(result.Error);
                    onComplete?.Invoke(result);
                    return;
                }

                var data = serializer.Deserialize(result.Data);
                if (data == null)
                {
                    Fail(onComplete, SaveErrorType.Corrupted,
                        $"Save in slot {slot} could not be parsed.");
                    return;
                }

                if (data.Metadata != null && data.Metadata.Version > Version)
                {
                    Fail(onComplete, SaveErrorType.VersionMismatch,
                        $"Save version {data.Metadata.Version} is newer than supported version {Version}.");
                    return;
                }

                try
                {
                    RestoreAll(data);
                }
                catch (Exception e)
                {
                    Fail(onComplete, SaveErrorType.Serialization, $"Failed to restore state: {e.Message}");
                    return;
                }

                OnLoaded?.Invoke(slot);
                onComplete?.Invoke(SaveResult.Ok());
            });
        }

        public void Delete(int slot, Action<SaveResult> onComplete = null)
        {
            if (!EnsureProvider(onComplete)) return;
            provider.Delete(SlotKey(slot), result =>
            {
                if (!result.Success) OnError?.Invoke(result.Error);
                onComplete?.Invoke(result);
            });
        }

        public void Exists(int slot, Action<bool> onComplete)
        {
            if (provider == null)
            {
                onComplete?.Invoke(false);
                return;
            }
            provider.Exists(SlotKey(slot), onComplete);
        }

        /// <summary>Reads a slot's metadata without applying it, for "load game" listings.</summary>
        public void GetMetadata(int slot, Action<SaveMetadata> onComplete)
        {
            if (provider == null)
            {
                onComplete?.Invoke(null);
                return;
            }
            provider.Read(SlotKey(slot), result =>
            {
                if (!result.Success)
                {
                    onComplete?.Invoke(null);
                    return;
                }
                var data = serializer.Deserialize(result.Data);
                onComplete?.Invoke(data?.Metadata);
            });
        }

        // --- Internals --------------------------------------------------------

        private SaveData CaptureAll(int slot)
        {
            double playtime = PlaytimeProvider != null ? PlaytimeProvider() : 0d;
            var data = new SaveData { Metadata = SaveMetadata.Create(slot, Version, playtime) };

            foreach (var pair in saveables)
            {
                data.Set(pair.Key, pair.Value.CaptureState());
            }
            return data;
        }

        private void RestoreAll(SaveData data)
        {
            foreach (var pair in saveables)
            {
                string json = data.Get(pair.Key);
                if (json != null) pair.Value.RestoreState(json);
            }
        }

        private bool EnsureProvider(Action<SaveResult> onComplete)
        {
            if (provider == null)
            {
                Fail(onComplete, SaveErrorType.ProviderNotAvailable,
                    "No save provider is registered. Assign one in the SaveManager inspector.");
                return false;
            }
            return true;
        }

        private void Fail(Action<SaveResult> onComplete, SaveErrorType type, string message)
        {
            var error = new SaveError(type, message);
            OnError?.Invoke(error);
            onComplete?.Invoke(SaveResult.Fail(error));
        }

        /// <summary>Stable storage key for a slot index, shared by every provider.</summary>
        public static string SlotKey(int slot) => $"slot_{slot}";
    }
}
