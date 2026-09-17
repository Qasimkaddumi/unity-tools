using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Binary side of <see cref="SaveService"/>: screenshots, thumbnails, recordings — data
    /// that belongs to a save slot but has no business inside the slot's JSON payload.
    ///
    /// <para>Blobs are deliberately <em>not</em> part of the <see cref="ISaveable"/> flow.
    /// A saveable captures itself on every save; a 300 KB screenshot should be written once,
    /// when the game decides to take it, and read back only when a menu actually shows it.
    /// So blobs are addressed directly by name and store, live under their own keys, and are
    /// swept up when their slot is deleted.</para>
    ///
    /// <para>A provider that implements <see cref="ISaveBlobProvider"/> stores the bytes
    /// natively. One that doesn't still works — the bytes go through base64 over the string
    /// channel, which is correct but roughly a third larger.</para>
    /// </summary>
    public partial class SaveService
    {
        /// <summary>
        /// Separates a slot's blob keys from its payload key. The '/' makes file-backed
        /// providers group a slot's blobs into their own folder.
        /// </summary>
        private const string BlobKeyInfix = "/blob.";

        // One warning per store, not one per screenshot.
        private readonly HashSet<string> warnedBase64Stores = new HashSet<string>();

        /// <summary>Storage key for a named blob belonging to a slot.</summary>
        public static string BlobKey(int slot, string name) => $"{SlotKey(slot)}{BlobKeyInfix}{name}";

        /// <summary>True when the key belongs to one of <paramref name="slot"/>'s blobs.</summary>
        private static bool IsBlobOf(int slot, string key) =>
            key != null && key.StartsWith(SlotKey(slot) + BlobKeyInfix, StringComparison.Ordinal);

        /// <summary>
        /// Writes <paramref name="bytes"/> as the blob <paramref name="name"/> for a slot.
        /// Unlike a slot save this targets exactly one store — you decide whether a
        /// screenshot is worth uploading — defaulting to the default store when
        /// <paramref name="storeId"/> is null.
        /// </summary>
        public void SaveBlob(int slot, string name, byte[] bytes, string storeId = null,
            Action<SaveResult> onComplete = null)
        {
            if (!TryResolveBlobTarget(name, storeId, out SaveStore store, out SaveResult failure))
            {
                onComplete?.Invoke(failure);
                return;
            }

            string key = BlobKey(slot, name);
            bytes = bytes ?? Array.Empty<byte>();

            if (store.Provider is ISaveBlobProvider blobProvider)
            {
                blobProvider.WriteBytes(key, bytes, result => onComplete?.Invoke(result));
                return;
            }

            WarnAboutBase64(store, bytes.Length);
            store.Provider.Write(key, Convert.ToBase64String(bytes), result => onComplete?.Invoke(result));
        }

        /// <summary>Reads the blob <paramref name="name"/> for a slot back as raw bytes.</summary>
        public void LoadBlob(int slot, string name, string storeId = null,
            Action<SaveBlobResult> onComplete = null)
        {
            if (!TryResolveBlobTarget(name, storeId, out SaveStore store, out SaveResult failure))
            {
                onComplete?.Invoke(SaveBlobResult.Fail(failure.Error));
                return;
            }

            string key = BlobKey(slot, name);

            if (store.Provider is ISaveBlobProvider blobProvider)
            {
                blobProvider.ReadBytes(key, result => onComplete?.Invoke(result));
                return;
            }

            store.Provider.Read(key, result =>
            {
                if (!result.Success)
                {
                    onComplete?.Invoke(SaveBlobResult.Fail(result.Error));
                    return;
                }

                try
                {
                    onComplete?.Invoke(SaveBlobResult.Ok(Convert.FromBase64String(result.Data ?? string.Empty)));
                }
                catch (FormatException e)
                {
                    onComplete?.Invoke(SaveBlobResult.Fail(SaveErrorType.Corrupted,
                        $"Blob '{name}' in store '{store.Id}' is not valid base64: {e.Message}"));
                }
            });
        }

        /// <summary>Deletes one named blob. Deleting one that was never written counts as success.</summary>
        public void DeleteBlob(int slot, string name, string storeId = null,
            Action<SaveResult> onComplete = null)
        {
            if (!TryResolveBlobTarget(name, storeId, out SaveStore store, out SaveResult failure))
            {
                onComplete?.Invoke(failure);
                return;
            }

            store.Provider.Delete(BlobKey(slot, name), result =>
            {
                bool alreadyGone = !result.Success && result.Error.Type == SaveErrorType.NotFound;
                onComplete?.Invoke(alreadyGone ? SaveResult.Ok() : result);
            });
        }

        /// <summary>True when the named blob exists in the given store.</summary>
        public void HasBlob(int slot, string name, string storeId = null, Action<bool> onComplete = null)
        {
            if (!TryResolveBlobTarget(name, storeId, out SaveStore store, out SaveResult _))
            {
                onComplete?.Invoke(false);
                return;
            }
            store.Provider.Exists(BlobKey(slot, name), onComplete);
        }

        /// <summary>
        /// Names of the blobs a slot has in the given store — enough to populate a save-slot
        /// screen without reading any of the bytes.
        /// </summary>
        public void ListBlobs(int slot, string storeId = null, Action<string[]> onComplete = null)
        {
            if (!TryResolveBlobTarget("_", storeId, out SaveStore store, out SaveResult _))
            {
                onComplete?.Invoke(Array.Empty<string>());
                return;
            }

            string prefix = SlotKey(slot) + BlobKeyInfix;
            store.Provider.List(keys =>
            {
                var names = new List<string>();
                if (keys != null)
                {
                    foreach (string key in keys)
                    {
                        if (IsBlobOf(slot, key)) names.Add(key.Substring(prefix.Length));
                    }
                }
                onComplete?.Invoke(names.ToArray());
            });
        }

        /// <summary>
        /// Removes every blob a slot owns in one store. Called as part of deleting the slot,
        /// so a deleted save can't leave its screenshots behind.
        /// </summary>
        private void DeleteBlobsOf(int slot, SaveStore store, Action onComplete)
        {
            store.Provider.List(keys =>
            {
                var doomed = new List<string>();
                if (keys != null)
                {
                    foreach (string key in keys)
                    {
                        if (IsBlobOf(slot, key)) doomed.Add(key);
                    }
                }

                if (doomed.Count == 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                int remaining = doomed.Count;
                foreach (string key in doomed)
                {
                    store.Provider.Delete(key, result =>
                    {
                        if (!result.Success && result.Error.Type != SaveErrorType.NotFound)
                        {
                            // The slot payload is already gone by this point, so a stray blob
                            // is litter rather than a failure worth failing the delete over.
                            Debug.LogWarning($"[SaveService] Could not delete blob '{key}' from store " +
                                             $"'{store.Id}': {result.Error}");
                        }
                        if (--remaining == 0) onComplete?.Invoke();
                    });
                }
            });
        }

        private bool TryResolveBlobTarget(string name, string storeId, out SaveStore store,
            out SaveResult failure)
        {
            store = null;
            failure = default;

            if (string.IsNullOrWhiteSpace(name))
            {
                failure = SaveResult.Fail(SaveErrorType.Unknown, "Blob name must not be empty.");
                return false;
            }

            store = GetStore(storeId ?? DefaultStoreId);
            if (store == null)
            {
                failure = SaveResult.Fail(SaveErrorType.StoreNotFound,
                    storeId == null
                        ? "No save store is registered. Add one in the SaveManager inspector."
                        : $"No save store is registered with id '{storeId}'.");
                return false;
            }
            return true;
        }

        private void WarnAboutBase64(SaveStore store, int byteCount)
        {
            if (!warnedBase64Stores.Add(store.Id)) return;

            Debug.LogWarning($"[SaveService] Store '{store.Id}' ({store.Provider.GetType().Name}) has no " +
                             "native binary support, so blobs are stored as base64 — about a third larger " +
                             $"(this one: {byteCount} bytes in, ~{byteCount * 4 / 3} out). Use a file-backed " +
                             "store for images, and avoid PlayerPrefs for them entirely.");
        }
    }
}
