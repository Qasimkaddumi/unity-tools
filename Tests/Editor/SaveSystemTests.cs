using Kaddumi.UnityTools.Save.Core;
using NUnit.Framework;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Kaddumi.SaveTests
{
    public class SaveSystemTests
    {
        private const int Slot = 0;

        private static SaveService NewService() => new SaveService();

        private static string PayloadOf(FakeProvider provider, int slot = Slot) =>
            provider.Store[SaveService.SlotKey(slot)];

        // --- Routing ----------------------------------------------------------

        [Test]
        public void Save_RoutesEachSaveableToItsOwnStore()
        {
            var service = NewService();
            var local = new FakeProvider();
            var cloud = new FakeProvider();
            service.RegisterStore(SaveStores.Local, local);
            service.RegisterStore(SaveStores.Cloud, cloud);

            service.RegisterSaveable(new FakeSaveable("gfx", SaveStores.Local, "settings"));
            service.RegisterSaveable(new FakeSaveable("progress", SaveStores.Cloud, "level9"));

            SaveReport report = null;
            service.Save(Slot, r => report = r);

            Assert.IsTrue(report.Success, report.ToString());
            Assert.That(PayloadOf(local), Does.Contain("gfx").And.Not.Contain("progress"));
            Assert.That(PayloadOf(cloud), Does.Contain("progress").And.Not.Contain("gfx"));
        }

        [Test]
        public void Save_UnknownStoreFallsBackToDefaultWithWarning()
        {
            var service = NewService();
            var local = new FakeProvider();
            service.RegisterStore(SaveStores.Local, local);
            service.RegisterSaveable(new FakeSaveable("orphan", "nowhere", "x"));

            LogAssert.Expect(LogType.Warning, new Regex("wants store 'nowhere'"));
            service.Save(Slot, null as Action<SaveReport>);

            Assert.That(PayloadOf(local), Does.Contain("orphan"));
        }

        [Test]
        public void Load_RestoresOnlySaveablesRoutedToThatStore()
        {
            var service = NewService();
            var local = new FakeProvider();
            var cloud = new FakeProvider();
            service.RegisterStore(SaveStores.Local, local);
            service.RegisterStore(SaveStores.Cloud, cloud);

            var gfx = new FakeSaveable("gfx", SaveStores.Local, "high");
            var progress = new FakeSaveable("progress", SaveStores.Cloud, "level9");
            service.RegisterSaveable(gfx);
            service.RegisterSaveable(progress);
            service.Save(Slot);

            gfx.Value = "changed";
            progress.Value = "changed";

            SaveReport report = null;
            service.Load(Slot, r => report = r);

            Assert.IsTrue(report.Success, report.ToString());
            Assert.AreEqual("high", gfx.Value);
            Assert.AreEqual("level9", progress.Value);
        }

        // --- Partial failure --------------------------------------------------

        [Test]
        public void Save_OptionalStoreFailureDoesNotFailTheSave()
        {
            var service = NewService();
            var local = new FakeProvider();
            var cloud = new FakeProvider { FailWith = SaveErrorType.Io };
            service.RegisterStore(SaveStores.Local, local);
            service.RegisterStore(SaveStores.Cloud, cloud, required: false);
            service.RegisterSaveable(new FakeSaveable("a", SaveStores.Local, "v"));

            SaveReport report = null;
            service.Save(Slot, r => report = r);

            Assert.IsTrue(report.Success, "an optional store must not sink the save");
            Assert.IsTrue(report.HasFailures);
            Assert.IsTrue(report.SucceededFor(SaveStores.Local));
            Assert.IsFalse(report.SucceededFor(SaveStores.Cloud));
        }

        [Test]
        public void Save_RequiredStoreFailureFailsTheSave()
        {
            var service = NewService();
            service.RegisterStore(SaveStores.Local, new FakeProvider());
            service.RegisterStore(SaveStores.Cloud, new FakeProvider { FailWith = SaveErrorType.Io });

            SaveReport report = null;
            service.Save(Slot, r => report = r);

            Assert.IsFalse(report.Success);
            Assert.AreEqual(SaveErrorType.Io, report.Error.Type);
        }

        // --- Mirrors ----------------------------------------------------------

        [Test]
        public void Save_MirrorReceivesACopyOfItsPrimarysData()
        {
            var service = NewService();
            var cloud = new FakeProvider();
            var cache = new FakeProvider();
            service.RegisterStore(SaveStores.Cloud, cloud);
            service.RegisterStore("cloud-cache", cache, mirrorOf: SaveStores.Cloud);
            service.RegisterSaveable(new FakeSaveable("progress", SaveStores.Cloud, "level9"));

            service.Save(Slot);

            Assert.That(PayloadOf(cache), Does.Contain("progress"));
            Assert.AreEqual(PayloadOf(cloud), PayloadOf(cache));
        }

        [Test]
        public void Save_WhilePrimaryIsOffline_MirrorStillKeepsTheData()
        {
            var service = NewService();
            var cloud = new FakeProvider { FailWith = SaveErrorType.Io };
            var cache = new FakeProvider();
            service.RegisterStore(SaveStores.Cloud, cloud, required: false);
            service.RegisterStore("cloud-cache", cache, mirrorOf: SaveStores.Cloud);
            service.RegisterSaveable(new FakeSaveable("progress", SaveStores.Cloud, "level9"));

            SaveReport report = null;
            service.Save(Slot, r => report = r);

            Assert.IsTrue(report.Success);
            Assert.That(PayloadOf(cache), Does.Contain("level9"));
        }

        [Test]
        public void Load_FallsBackToTheMirrorWhenThePrimaryIsUnreachable()
        {
            var service = NewService();
            var cloud = new FakeProvider();
            var cache = new FakeProvider();
            service.RegisterStore(SaveStores.Cloud, cloud);
            service.RegisterStore("cloud-cache", cache, mirrorOf: SaveStores.Cloud);

            var progress = new FakeSaveable("progress", SaveStores.Cloud, "level9");
            service.RegisterSaveable(progress);
            service.Save(Slot);

            cloud.FailWith = SaveErrorType.Io;       // go offline
            progress.Value = "wiped";

            SaveReport report = null;
            LogAssert.Expect(LogType.Log, new Regex("loaded from its mirror"));
            service.Load(Slot, r => report = r);

            Assert.IsTrue(report.Success, report.ToString());
            Assert.AreEqual("level9", progress.Value);
        }

        // --- Sync -------------------------------------------------------------

        private static (SaveService service, FakeProvider cloud, FakeProvider cache, FakeSaveable saveable)
            MirroredSetup()
        {
            var service = NewService();
            var cloud = new FakeProvider();
            var cache = new FakeProvider();
            service.RegisterStore(SaveStores.Cloud, cloud);
            service.RegisterStore("cloud-cache", cache, mirrorOf: SaveStores.Cloud);

            var saveable = new FakeSaveable("progress", SaveStores.Cloud, "start");
            service.RegisterSaveable(saveable);
            return (service, cloud, cache, saveable);
        }

        [Test]
        public void Sync_WithNothingAnywhereReportsNoData()
        {
            var (service, _, _, _) = MirroredSetup();

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.AreEqual(SaveSyncStatus.NoData, result.Status);
        }

        [Test]
        public void Sync_WhenSidesMatchReportsInSync()
        {
            var (service, _, _, _) = MirroredSetup();
            service.Save(Slot);

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.AreEqual(SaveSyncStatus.InSync, result.Status);
            Assert.IsFalse(result.HadConflict);
        }

        [Test]
        public void Sync_AfterAnOfflineSaveCopiesToTheCloudWithoutClaimingAConflict()
        {
            var (service, cloud, cache, _) = MirroredSetup();

            service.Save(Slot);                                   // both sides agree
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache");  // record the agreement

            cloud.FailWith = SaveErrorType.Io;                    // go offline
            System.Threading.Thread.Sleep(15);                    // a distinct timestamp
            service.Save(Slot);                                   // only the cache updates
            cloud.FailWith = null;                                // back online

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.AreEqual(SaveSyncStatus.CopiedToPrimary, result.Status,
                "the cache holds the newer save, so it is copied onto the cloud");
            Assert.IsFalse(result.HadConflict, "the cloud was merely behind; that is not a conflict");
            Assert.AreEqual(PayloadOf(cache), PayloadOf(cloud));
        }

        [Test]
        public void Sync_WhenTheCloudMovedAheadCopiesToTheCacheAndApplies()
        {
            var (service, cloud, cache, saveable) = MirroredSetup();

            service.Save(Slot);
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache");

            // Another device wrote a newer save straight into the cloud.
            System.Threading.Thread.Sleep(15);
            cloud.Store[SaveService.SlotKey(Slot)] =
                PayloadOf(cache).Replace("start", "fromOtherDevice")
                                .Replace(MetadataStampOf(PayloadOf(cache)), DateTime.UtcNow.ToString("o"));

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.AreEqual(SaveSyncStatus.CopiedToMirror, result.Status);
            Assert.IsFalse(result.HadConflict);
            Assert.IsTrue(result.Applied, "the primary changed, so live objects are refreshed");
            Assert.AreEqual("fromOtherDevice", saveable.Value);
        }

        [Test]
        public void Sync_WhenBothSidesMovedReportsAConflictAndNewestWins()
        {
            var (service, cloud, cache, _) = MirroredSetup();

            service.Save(Slot);
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache");

            // Both sides move on independently; the cache ends up newer.
            cloud.Store[SaveService.SlotKey(Slot)] =
                PayloadOf(cache).Replace("start", "otherDevice")
                                .Replace(MetadataStampOf(PayloadOf(cache)),
                                         DateTime.UtcNow.AddMinutes(-5).ToString("o"));
            System.Threading.Thread.Sleep(15);
            service.Save(Slot, new[] { "cloud-cache" });

            SaveConflict seen = null;
            service.OnConflict += c => seen = c;

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.IsTrue(result.HadConflict, "both sides changed since they last agreed");
            Assert.IsNotNull(seen, "OnConflict should fire so a game can tell the player");
            Assert.AreEqual(SaveSyncStatus.CopiedToPrimary, result.Status,
                "the cache is newer, so newest-wins copies it onto the cloud");
        }

        [Test]
        public void Sync_ManualPolicyCanDeferWithoutTouchingEitherSide()
        {
            var (service, cloud, cache, _) = MirroredSetup();
            service.Save(Slot);
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache");

            cloud.Store[SaveService.SlotKey(Slot)] =
                PayloadOf(cache).Replace("start", "otherDevice")
                                .Replace(MetadataStampOf(PayloadOf(cache)),
                                         DateTime.UtcNow.AddMinutes(-5).ToString("o"));
            System.Threading.Thread.Sleep(15);
            service.Save(Slot, new[] { "cloud-cache" });

            string cloudBefore = PayloadOf(cloud);
            string cacheBefore = PayloadOf(cache);

            service.ConflictPolicy = SaveConflictPolicy.Manual;
            service.ConflictResolver = _ => SaveSyncDirection.None;

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.AreEqual(SaveSyncStatus.Deferred, result.Status);
            Assert.AreEqual(cloudBefore, PayloadOf(cloud), "deferring must not write anything");
            Assert.AreEqual(cacheBefore, PayloadOf(cache), "deferring must not write anything");
        }

        [Test]
        public void Sync_WithAnUnreadableSideFailsAndChangesNothing()
        {
            var (service, cloud, cache, _) = MirroredSetup();
            service.Save(Slot);
            string cacheBefore = PayloadOf(cache);

            cloud.FailWith = SaveErrorType.Io;

            SaveSyncResult result = null;
            service.Sync(Slot, SaveStores.Cloud, "cloud-cache", true, r => result = r);

            Assert.AreEqual(SaveSyncStatus.Failed, result.Status);
            Assert.AreEqual(cacheBefore, PayloadOf(cache),
                "an unreachable cloud must never cause the local copy to be overwritten");
        }

        [Test]
        public void SyncAll_CoversEveryDeclaredMirrorPair()
        {
            var service = NewService();
            service.RegisterStore(SaveStores.Local, new FakeProvider());
            service.RegisterStore(SaveStores.Cloud, new FakeProvider());
            service.RegisterStore("cloud-cache", new FakeProvider(), mirrorOf: SaveStores.Cloud);
            service.RegisterSaveable(new FakeSaveable("p", SaveStores.Cloud, "v"));
            service.Save(Slot);

            SaveSyncResult[] results = null;
            service.SyncAll(Slot, true, r => results = r);

            Assert.AreEqual(1, results.Length, "only the cloud has a mirror");
            Assert.AreEqual(SaveSyncStatus.InSync, results[0].Status);
        }

        /// <summary>Pulls the SavedAtUtc value out of a serialized payload, for test fixtures.</summary>
        private static string MetadataStampOf(string payload)
        {
            Match m = Regex.Match(payload, "\"SavedAtUtc\":\"([^\"]+)\"");
            Assert.IsTrue(m.Success, "payload should carry a SavedAtUtc: " + payload);
            return m.Groups[1].Value;
        }

        // --- Blobs ------------------------------------------------------------

        [Test]
        public void Blob_RoundTripsThroughANativeBinaryProvider()
        {
            var service = NewService();
            var store = new FakeBlobProvider();
            service.RegisterStore(SaveStores.Local, store);

            byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF };
            service.SaveBlob(Slot, "screenshot", png);

            SaveBlobResult result = default;
            service.LoadBlob(Slot, "screenshot", null, r => result = r);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(png, result.Data);
            Assert.AreEqual(0, store.Store[SaveService.BlobKey(Slot, "screenshot")].Length - "<bytes>".Length,
                "a native provider should have taken the byte path");
        }

        [Test]
        public void Blob_RoundTripsThroughTheBase64FallbackAndWarnsOnce()
        {
            var service = NewService();
            var store = new FakeProvider();          // string-only, no ISaveBlobProvider
            service.RegisterStore(SaveStores.Local, store);

            byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF };

            LogAssert.Expect(LogType.Warning, new Regex("no native binary support"));
            service.SaveBlob(Slot, "screenshot", png);
            service.SaveBlob(Slot, "second", png);   // must not warn again

            SaveBlobResult result = default;
            service.LoadBlob(Slot, "screenshot", null, r => result = r);

            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(png, result.Data);
            Assert.AreEqual(Convert.ToBase64String(png), store.Store[SaveService.BlobKey(Slot, "screenshot")]);
        }

        [Test]
        public void Blob_IsNotMixedIntoTheSlotPayload()
        {
            var service = NewService();
            var store = new FakeBlobProvider();
            service.RegisterStore(SaveStores.Local, store);
            service.RegisterSaveable(new FakeSaveable("a", SaveStores.Local, "v"));

            service.SaveBlob(Slot, "screenshot", Encoding.UTF8.GetBytes("IMAGE"));
            service.Save(Slot);

            Assert.That(PayloadOf(store), Does.Not.Contain("IMAGE"));
            Assert.AreNotEqual(SaveService.SlotKey(Slot), SaveService.BlobKey(Slot, "screenshot"));
        }

        [Test]
        public void ListBlobs_ReturnsNamesForThatSlotOnly()
        {
            var service = NewService();
            service.RegisterStore(SaveStores.Local, new FakeBlobProvider());

            service.SaveBlob(0, "screenshot", new byte[] { 1 });
            service.SaveBlob(0, "thumb", new byte[] { 2 });
            service.SaveBlob(1, "screenshot", new byte[] { 3 });

            string[] names = null;
            service.ListBlobs(0, null, n => names = n);

            CollectionAssert.AreEquivalent(new[] { "screenshot", "thumb" }, names);
        }

        [Test]
        public void Delete_SweepsTheSlotsBlobsToo()
        {
            var service = NewService();
            var store = new FakeBlobProvider();
            service.RegisterStore(SaveStores.Local, store);
            service.RegisterSaveable(new FakeSaveable("a", SaveStores.Local, "v"));

            service.Save(0);
            service.SaveBlob(0, "screenshot", new byte[] { 1 });
            service.SaveBlob(1, "screenshot", new byte[] { 2 });

            SaveReport report = null;
            service.Delete(0, r => report = r);

            Assert.IsTrue(report.Success, report.ToString());
            Assert.IsFalse(store.Store.ContainsKey(SaveService.BlobKey(0, "screenshot")),
                "slot 0's blob should be gone");
            Assert.IsTrue(store.Store.ContainsKey(SaveService.BlobKey(1, "screenshot")),
                "slot 1's blob must be untouched");
        }

        [Test]
        public void Blob_UnknownStoreFailsCleanly()
        {
            var service = NewService();
            service.RegisterStore(SaveStores.Local, new FakeBlobProvider());

            SaveResult result = default;
            service.SaveBlob(Slot, "shot", new byte[] { 1 }, "nowhere", r => result = r);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveErrorType.StoreNotFound, result.Error.Type);
        }
    }
}

