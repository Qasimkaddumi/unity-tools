using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Reconciliation half of <see cref="SaveService"/>: keeps a store and its mirror in
    /// agreement about a slot, and decides what happens when they disagree.
    ///
    /// <para>The shape of the problem, for a cloud store with a local file mirror: every
    /// save writes both, but the cloud write is allowed to fail (offline), and the cloud
    /// copy can also move ahead on its own when the player plays on another device. So on
    /// any given launch either side may be the fresher one, or both may have changed since
    /// they last agreed — a genuine conflict.</para>
    ///
    /// <para>Sync reads the <see cref="SaveMetadata"/> from both sides, picks a winner by
    /// <see cref="SaveService.ConflictPolicy"/> (or asks
    /// <see cref="SaveService.ConflictResolver"/>), and copies the winning payload over the
    /// loser verbatim. It never merges: a half-merged save is worse than either input.</para>
    ///
    /// <para>"Both changed" is the part that needs care. Two differing saves are not
    /// automatically a conflict — usually one side is just behind, which is what an offline
    /// session leaves behind and should resolve silently. To tell the two apart, each sync
    /// records a token on the mirror: the timestamp of the payload both sides held when they
    /// last agreed. A side whose save still carries that timestamp hasn't changed since, so
    /// only when <em>neither</em> does (or there is no token yet) is it a real conflict worth
    /// bothering the player about.</para>
    /// </summary>
    public partial class SaveService
    {
        /// <summary>How a slot that changed on both sides is settled. Defaults to newest-wins.</summary>
        public SaveConflictPolicy ConflictPolicy { get; set; } = SaveConflictPolicy.NewestWins;

        /// <summary>
        /// Called to settle a conflict when <see cref="ConflictPolicy"/> is
        /// <see cref="SaveConflictPolicy.Manual"/> — the hook for a "your cloud save is
        /// newer, keep which?" prompt. Returning <see cref="SaveSyncDirection.None"/> leaves
        /// both sides untouched and reports <see cref="SaveSyncStatus.Deferred"/>, which is
        /// what you want while a dialog is still on screen.
        ///
        /// <para>It is called synchronously, so it must decide without waiting. To ask the
        /// player, return <c>None</c> now and call Sync again once they have answered, with
        /// the policy set to <see cref="SaveConflictPolicy.PreferPrimary"/> or
        /// <see cref="SaveConflictPolicy.PreferMirror"/> to carry out their choice.</para>
        /// </summary>
        public Func<SaveConflict, SaveSyncDirection> ConflictResolver { get; set; }

        /// <summary>Raised whenever a sync finds both sides changed, before it is resolved.</summary>
        public event Action<SaveConflict> OnConflict;

        /// <summary>
        /// Reconciles one slot between a store and its mirror.
        /// </summary>
        /// <param name="applyToLiveObjects">
        /// Whether the winning save is applied to the registered saveables. True is right at
        /// startup, where in-memory state is placeholder anyway; pass false when syncing
        /// mid-session, so a background sync can't yank the player's state out from under them.
        /// </param>
        public void Sync(int slot, string primaryStoreId, string mirrorStoreId,
            bool applyToLiveObjects = true, Action<SaveSyncResult> onComplete = null)
        {
            SaveStore primary = GetStore(primaryStoreId);
            SaveStore mirror = GetStore(mirrorStoreId);

            if (primary == null || mirror == null)
            {
                string missing = primary == null ? primaryStoreId : mirrorStoreId;
                onComplete?.Invoke(SaveSyncResult.Fail(slot, primaryStoreId, mirrorStoreId,
                    SaveErrorType.StoreNotFound, $"No save store is registered with id '{missing}'."));
                return;
            }
            if (primary == mirror)
            {
                onComplete?.Invoke(SaveSyncResult.Fail(slot, primaryStoreId, mirrorStoreId,
                    SaveErrorType.Unknown, "A store cannot be synced with itself."));
                return;
            }

            ReadAgreedToken(mirror, slot, token =>
                ReadSide(primary, slot, primarySide =>
                    ReadSide(mirror, slot, mirrorSide =>
                        Reconcile(slot, primary, mirror, primarySide, mirrorSide, token,
                            applyToLiveObjects, onComplete))));
        }

        /// <summary>
        /// Reconciles every declared mirror pair for a slot. This is the one to call on
        /// startup, after sign-in, or behind a "sync now" button — it needs no knowledge of
        /// how the stores are wired, because the mirror relationships come from the inspector.
        /// </summary>
        public void SyncAll(int slot, bool applyToLiveObjects = true,
            Action<SaveSyncResult[]> onComplete = null)
        {
            var pairs = new List<SaveStore>();
            foreach (SaveStore store in stores)
            {
                if (store.IsMirror && HasStore(store.MirrorOf)) pairs.Add(store);
            }

            if (pairs.Count == 0)
            {
                onComplete?.Invoke(Array.Empty<SaveSyncResult>());
                return;
            }

            var results = new SaveSyncResult[pairs.Count];
            int remaining = pairs.Count;

            for (int i = 0; i < pairs.Count; i++)
            {
                int index = i;
                Sync(slot, pairs[i].MirrorOf, pairs[i].Id, applyToLiveObjects, result =>
                {
                    results[index] = result;
                    if (--remaining == 0) onComplete?.Invoke(results);
                });
            }
        }

        // --- Internals --------------------------------------------------------

        /// <summary>
        /// Where a mirror records what it last agreed with its primary on. It lives in the
        /// mirror (the local side) on purpose: the question it answers — "what did
        /// <em>this device</em> last see both sides holding?" — is per-device.
        /// </summary>
        internal static string SyncTokenKey(int slot) => $"{SlotKey(slot)}/sync.token";

        private void ReadAgreedToken(SaveStore mirror, int slot, Action<string> onComplete)
        {
            mirror.Provider.Read(SyncTokenKey(slot), result =>
                onComplete(result.Success ? result.Data : null));
        }

        /// <summary>
        /// Records the timestamp both sides now hold. Best effort: a token that fails to
        /// write only costs a spurious conflict prompt on the next sync, which is not worth
        /// failing an otherwise-good sync over.
        /// </summary>
        private void WriteAgreedToken(SaveStore mirror, int slot, string savedAtUtc, Action onComplete)
        {
            if (string.IsNullOrEmpty(savedAtUtc))
            {
                onComplete();
                return;
            }

            mirror.Provider.Write(SyncTokenKey(slot), savedAtUtc, result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"[SaveService] Could not record the sync token for slot {slot} in " +
                                     $"store '{mirror.Id}': {result.Error}. The next sync may report a " +
                                     "conflict that isn't one.");
                }
                onComplete();
            });
        }

        /// <summary>One side of a sync: the raw payload and the metadata parsed out of it.</summary>
        private struct SyncSide
        {
            public bool Present;
            public string Payload;
            public SaveMetadata Metadata;
            public SaveError Error;

            /// <summary>
            /// True when the read failed for a reason other than "there is nothing here" —
            /// an offline backend or a corrupt file. Distinguishing this from an empty store
            /// matters: an empty store is a normal first-run state, an unreachable one is not
            /// and must never be treated as "the other side should overwrite it".
            /// </summary>
            public bool Unreachable;
        }

        private void ReadSide(SaveStore store, int slot, Action<SyncSide> onComplete)
        {
            store.Provider.Read(SlotKey(slot), result =>
            {
                if (!result.Success)
                {
                    onComplete(new SyncSide
                    {
                        Present = false,
                        Error = result.Error,
                        Unreachable = result.Error.Type != SaveErrorType.NotFound
                    });
                    return;
                }

                SaveData data = serializer.Deserialize(result.Data);
                if (data?.Metadata == null)
                {
                    // Readable but unusable. Treated as unreachable rather than empty, so a
                    // corrupt file is never silently overwritten by a sync the player
                    // didn't ask for.
                    onComplete(new SyncSide
                    {
                        Present = false,
                        Unreachable = true,
                        Error = new SaveError(SaveErrorType.Corrupted,
                            $"Save in slot {slot} (store '{store.Id}') could not be parsed.")
                    });
                    return;
                }

                onComplete(new SyncSide
                {
                    Present = true,
                    Payload = result.Data,
                    Metadata = data.Metadata
                });
            });
        }

        private void Reconcile(int slot, SaveStore primary, SaveStore mirror,
            SyncSide primarySide, SyncSide mirrorSide, string agreedToken, bool applyToLiveObjects,
            Action<SaveSyncResult> onComplete)
        {
            var result = new SaveSyncResult
            {
                Slot = slot,
                PrimaryStoreId = primary.Id,
                MirrorStoreId = mirror.Id,
                Primary = primarySide.Metadata,
                Mirror = mirrorSide.Metadata
            };

            // Refuse to act on a side we couldn't read properly — overwriting it would
            // destroy a save that may be perfectly fine once the network is back.
            if (primarySide.Unreachable || mirrorSide.Unreachable)
            {
                SyncSide broken = primarySide.Unreachable ? primarySide : mirrorSide;
                string brokenId = primarySide.Unreachable ? primary.Id : mirror.Id;
                result.Status = SaveSyncStatus.Failed;
                result.Error = new SaveError(broken.Error.Type,
                    $"Cannot sync slot {slot}: store '{brokenId}' could not be read " +
                    $"({broken.Error.Message}). Nothing was changed.", broken.Error.Code);
                Finish(result, onComplete);
                return;
            }

            if (!primarySide.Present && !mirrorSide.Present)
            {
                result.Status = SaveSyncStatus.NoData;
                Finish(result, onComplete);
                return;
            }

            SaveSyncDirection direction;

            if (primarySide.Present && !mirrorSide.Present)
            {
                direction = SaveSyncDirection.ToMirror;
            }
            else if (!primarySide.Present)
            {
                direction = SaveSyncDirection.ToPrimary;
            }
            else if (string.Equals(primarySide.Payload, mirrorSide.Payload, StringComparison.Ordinal))
            {
                result.Status = SaveSyncStatus.InSync;
                // Both sides already match — record it, so the next divergence can be
                // attributed to whichever side moved.
                WriteAgreedToken(mirror, slot, primarySide.Metadata.SavedAtUtc,
                    () => Finish(result, onComplete));
                return;
            }
            else
            {
                // Two different saves. Only one that neither side can account for against the
                // last agreement is a real conflict.
                bool primaryMoved = !StampMatches(primarySide, agreedToken);
                bool mirrorMoved = !StampMatches(mirrorSide, agreedToken);

                if (agreedToken != null && primaryMoved && !mirrorMoved)
                {
                    direction = SaveSyncDirection.ToMirror;
                }
                else if (agreedToken != null && mirrorMoved && !primaryMoved)
                {
                    direction = SaveSyncDirection.ToPrimary;
                }
                else
                {
                    // Both moved, or there is no token yet and we simply can't tell.
                    result.HadConflict = true;
                    direction = ResolveConflict(slot, primary, mirror, primarySide, mirrorSide);

                    if (direction == SaveSyncDirection.None)
                    {
                        result.Status = SaveSyncStatus.Deferred;
                        Finish(result, onComplete);
                        return;
                    }
                }
            }

            SyncSide winner = direction == SaveSyncDirection.ToMirror ? primarySide : mirrorSide;
            SaveStore target = direction == SaveSyncDirection.ToMirror ? mirror : primary;

            // Never copy a save this build can't read onto a slot that currently holds one
            // it can. The newer build will sort it out; this one must not make it worse.
            if (winner.Metadata.Version > Version)
            {
                result.Status = SaveSyncStatus.Failed;
                result.Error = new SaveError(SaveErrorType.VersionMismatch,
                    $"The winning save for slot {slot} is version {winner.Metadata.Version}, newer than " +
                    $"the supported version {Version}. Nothing was changed.");
                Finish(result, onComplete);
                return;
            }

            target.Provider.Write(SlotKey(slot), winner.Payload, writeResult =>
            {
                if (!writeResult.Success)
                {
                    result.Status = SaveSyncStatus.Failed;
                    result.Error = writeResult.Error;
                    Finish(result, onComplete);
                    return;
                }

                result.Status = direction == SaveSyncDirection.ToMirror
                    ? SaveSyncStatus.CopiedToMirror
                    : SaveSyncStatus.CopiedToPrimary;

                // Apply the winner whichever way it went. Direction says which store was
                // written, not whether the live objects are stale — and the case that most
                // needs refreshing is the primary having moved ahead on another device,
                // where the copy goes the other way. When the winner is what memory already
                // holds, re-applying it is a no-op.
                if (applyToLiveObjects)
                {
                    result.Applied = TryApply(slot, primary, winner.Payload, result);
                }

                // Both sides now hold the winner: that is the new point of agreement.
                WriteAgreedToken(mirror, slot, winner.Metadata.SavedAtUtc,
                    () => Finish(result, onComplete));
            });
        }

        /// <summary>
        /// True when this side's save is still the one recorded at the last agreement, i.e.
        /// it has not been written since.
        /// </summary>
        private static bool StampMatches(SyncSide side, string agreedToken) =>
            agreedToken != null && side.Metadata != null &&
            string.Equals(side.Metadata.SavedAtUtc, agreedToken, StringComparison.Ordinal);

        private SaveSyncDirection ResolveConflict(int slot, SaveStore primary, SaveStore mirror,
            SyncSide primarySide, SyncSide mirrorSide)
        {
            var conflict = new SaveConflict
            {
                Slot = slot,
                PrimaryStoreId = primary.Id,
                MirrorStoreId = mirror.Id,
                Primary = primarySide.Metadata,
                Mirror = mirrorSide.Metadata
            };

            OnConflict?.Invoke(conflict);

            switch (ConflictPolicy)
            {
                case SaveConflictPolicy.PreferPrimary:
                    return SaveSyncDirection.ToMirror;

                case SaveConflictPolicy.PreferMirror:
                    return SaveSyncDirection.ToPrimary;

                case SaveConflictPolicy.MostPlaytime:
                    return primarySide.Metadata.PlaytimeSeconds >= mirrorSide.Metadata.PlaytimeSeconds
                        ? SaveSyncDirection.ToMirror
                        : SaveSyncDirection.ToPrimary;

                case SaveConflictPolicy.Manual:
                    if (ConflictResolver != null) return ConflictResolver(conflict);
                    Debug.LogWarning("[SaveService] ConflictPolicy is Manual but no ConflictResolver is " +
                                     $"assigned. Falling back to newest-wins for {conflict}.");
                    return conflict.NewestWins;

                default:
                    return conflict.NewestWins;
            }
        }

        /// <summary>Restores the saveables routed to <paramref name="primary"/> from its new payload.</summary>
        private bool TryApply(int slot, SaveStore primary, string payload, SaveSyncResult result)
        {
            SaveData data = serializer.Deserialize(payload);
            if (data == null) return false;

            try
            {
                Dictionary<string, List<ISaveable>> routed = GroupSaveablesByStore();
                routed.TryGetValue(primary.Id, out List<ISaveable> owned);
                Restore(data, owned);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Copied slot {slot} into '{primary.Id}' but could not " +
                                 $"apply it to the live objects: {e.Message}");
                return false;
            }

            OnLoaded?.Invoke(slot);
            return true;
        }

        private void Finish(SaveSyncResult result, Action<SaveSyncResult> onComplete)
        {
            if (!result.Success) OnError?.Invoke(result.Error);
            onComplete?.Invoke(result);
        }
    }
}
