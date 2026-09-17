using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;

namespace Kaddumi.SaveTests
{
    /// <summary>In-memory provider standing in for a real backend. Callbacks are synchronous.</summary>
    public class FakeProvider : ISaveProvider
    {
        public readonly Dictionary<string, string> Store = new Dictionary<string, string>();

        /// <summary>When set, every operation fails with this error (simulates being offline).</summary>
        public SaveErrorType? FailWith;

        public int Writes, Reads;

        public bool IsInitialized { get; private set; }

        public void Initialize(Action onComplete)
        {
            IsInitialized = true;
            onComplete?.Invoke();
        }

        public void Write(string key, string data, Action<SaveResult> onComplete)
        {
            Writes++;
            if (FailWith.HasValue)
            {
                onComplete(SaveResult.Fail(FailWith.Value, "offline"));
                return;
            }
            Store[key] = data;
            onComplete(SaveResult.Ok());
        }

        public void Read(string key, Action<SaveResult> onComplete)
        {
            Reads++;
            if (FailWith.HasValue)
            {
                onComplete(SaveResult.Fail(FailWith.Value, "offline"));
                return;
            }
            onComplete(Store.TryGetValue(key, out string v)
                ? SaveResult.Ok(v)
                : SaveResult.Fail(SaveErrorType.NotFound, key));
        }

        public void Delete(string key, Action<SaveResult> onComplete)
        {
            if (FailWith.HasValue)
            {
                onComplete(SaveResult.Fail(FailWith.Value, "offline"));
                return;
            }
            onComplete(Store.Remove(key) ? SaveResult.Ok() : SaveResult.Fail(SaveErrorType.NotFound, key));
        }

        public void Exists(string key, Action<bool> onComplete) =>
            onComplete(!FailWith.HasValue && Store.ContainsKey(key));

        public void List(Action<string[]> onComplete)
        {
            if (FailWith.HasValue)
            {
                onComplete(Array.Empty<string>());
                return;
            }
            var keys = new string[Store.Count];
            Store.Keys.CopyTo(keys, 0);
            onComplete(keys);
        }
    }

    /// <summary>A provider that also stores raw bytes, like the file-backed ones do.</summary>
    public class FakeBlobProvider : FakeProvider, ISaveBlobProvider
    {
        public readonly Dictionary<string, byte[]> Bytes = new Dictionary<string, byte[]>();

        public void WriteBytes(string key, byte[] data, Action<SaveResult> onComplete)
        {
            Bytes[key] = data;
            // Mirror into the string store so List/Delete see the key too.
            Store[key] = "<bytes>";
            onComplete(SaveResult.Ok());
        }

        public void ReadBytes(string key, Action<SaveBlobResult> onComplete) =>
            onComplete(Bytes.TryGetValue(key, out byte[] v)
                ? SaveBlobResult.Ok(v)
                : SaveBlobResult.Fail(SaveErrorType.NotFound, key));
    }

    /// <summary>Saveable whose state is a single string, routed to a store of the test's choosing.</summary>
    public class FakeSaveable : ISaveable
    {
        [Serializable]
        public class State { public string Value; }

        public FakeSaveable(string key, string storageId, string value = "")
        {
            SaveKey = key;
            StorageId = storageId;
            Value = value;
        }

        public string SaveKey { get; }
        public string StorageId { get; }
        public string Value;
        public int Restores;

        public string CaptureState() => UnityEngine.JsonUtility.ToJson(new State { Value = Value });

        public void RestoreState(string state)
        {
            Value = UnityEngine.JsonUtility.FromJson<State>(state).Value;
            Restores++;
        }
    }
}
