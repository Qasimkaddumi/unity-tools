using System;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>How to settle a slot that changed on both sides since the last sync.</summary>
    public enum SaveConflictPolicy
    {
        /// <summary>Later <see cref="SaveMetadata.SavedAtUtc"/> wins. The sane default.</summary>
        NewestWins,

        /// <summary>
        /// Higher <see cref="SaveMetadata.PlaytimeSeconds"/> wins. Better than timestamps when
        /// device clocks can't be trusted, and closer to "which save did more work".
        /// </summary>
        MostPlaytime,

        /// <summary>Always keep the primary store's copy.</summary>
        PreferPrimary,

        /// <summary>Always keep the mirror store's copy.</summary>
        PreferMirror,

        /// <summary>
        /// Hand the decision to <c>SaveService.ConflictResolver</c> — a "cloud save is newer,
        /// keep which?" prompt. Without a resolver assigned this behaves like
        /// <see cref="NewestWins"/>.
        /// </summary>
        Manual
    }

    /// <summary>
    /// Which way a sync copied data. Named for the two stores rather than "push"/"pull",
    /// because which of them is the remote one is up to how you wired them: in the usual
    /// setup the cloud store is the <em>primary</em> and the local cache is its mirror, so
    /// "push" would read backwards.
    /// </summary>
    public enum SaveSyncDirection
    {
        /// <summary>Leave both sides alone.</summary>
        None,

        /// <summary>Copy the primary store's save over the mirror's.</summary>
        ToMirror,

        /// <summary>Copy the mirror store's save over the primary's.</summary>
        ToPrimary
    }

    /// <summary>Outcome of reconciling one slot across two stores.</summary>
    public enum SaveSyncStatus
    {
        /// <summary>Neither store holds this slot; nothing to do.</summary>
        NoData,

        /// <summary>Both sides already agree.</summary>
        InSync,

        /// <summary>The primary's save was copied to the mirror.</summary>
        CopiedToMirror,

        /// <summary>The mirror's save was copied to the primary — e.g. a local cache catching the cloud up.</summary>
        CopiedToPrimary,

        /// <summary>A conflict was left unresolved, because a manual resolver declined to pick.</summary>
        Deferred,

        /// <summary>The sync could not be completed; see <see cref="SaveSyncResult.Error"/>.</summary>
        Failed
    }

    /// <summary>
    /// The facts of a disagreement between two stores, handed to a manual resolver so a
    /// game can put a "which save do you want to keep?" dialog in front of the player.
    /// Both metadata objects are non-null here: a conflict means both sides have a save.
    /// </summary>
    public class SaveConflict
    {
        public int Slot { get; internal set; }

        public string PrimaryStoreId { get; internal set; }
        public string MirrorStoreId { get; internal set; }

        public SaveMetadata Primary { get; internal set; }
        public SaveMetadata Mirror { get; internal set; }

        /// <summary>How much later the primary's save is than the mirror's; negative if older.</summary>
        public TimeSpan PrimaryIsNewerBy => Primary.SavedAt() - Mirror.SavedAt();

        /// <summary>What <see cref="SaveConflictPolicy.NewestWins"/> would choose, as a starting point.</summary>
        public SaveSyncDirection NewestWins =>
            Primary.SavedAt() >= Mirror.SavedAt() ? SaveSyncDirection.ToMirror : SaveSyncDirection.ToPrimary;

        public override string ToString() =>
            $"slot {Slot}: {PrimaryStoreId} @ {Primary.SavedAtUtc} ({Primary.PlaytimeSeconds:F0}s) vs " +
            $"{MirrorStoreId} @ {Mirror.SavedAtUtc} ({Mirror.PlaytimeSeconds:F0}s)";
    }

    /// <summary>
    /// Result of a <c>SaveService.Sync</c>. <see cref="Status"/> says what happened;
    /// <see cref="HadConflict"/> distinguishes "one side simply had nothing" from "both sides
    /// had a save and one of them lost", which is the case worth telling the player about.
    /// </summary>
    public class SaveSyncResult
    {
        public SaveSyncStatus Status { get; internal set; }

        public int Slot { get; internal set; }

        public string PrimaryStoreId { get; internal set; }
        public string MirrorStoreId { get; internal set; }

        /// <summary>Metadata found in each store before the sync; null where there was no save.</summary>
        public SaveMetadata Primary { get; internal set; }
        public SaveMetadata Mirror { get; internal set; }

        /// <summary>True when both stores held a save and they disagreed.</summary>
        public bool HadConflict { get; internal set; }

        /// <summary>Set when <see cref="Status"/> is <see cref="SaveSyncStatus.Failed"/>.</summary>
        public SaveError Error { get; internal set; }

        /// <summary>True when the sync ran to completion, including the "nothing to do" outcomes.</summary>
        public bool Success => Status != SaveSyncStatus.Failed;

        /// <summary>True when the winning save was applied to the registered saveables.</summary>
        public bool Applied { get; internal set; }

        internal static SaveSyncResult Fail(int slot, string primaryId, string mirrorId, SaveError error) =>
            new SaveSyncResult
            {
                Status = SaveSyncStatus.Failed,
                Slot = slot,
                PrimaryStoreId = primaryId,
                MirrorStoreId = mirrorId,
                Error = error
            };

        internal static SaveSyncResult Fail(int slot, string primaryId, string mirrorId,
            SaveErrorType type, string message) =>
            Fail(slot, primaryId, mirrorId, new SaveError(type, message));

        public override string ToString()
        {
            string suffix = Status == SaveSyncStatus.Failed ? $" ({Error})"
                : HadConflict ? " (conflict resolved)"
                : string.Empty;
            return $"{Status} {PrimaryStoreId}<->{MirrorStoreId} slot {Slot}{suffix}";
        }
    }
}
