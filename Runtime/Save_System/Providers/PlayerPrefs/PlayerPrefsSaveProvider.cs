using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    /// <summary>
    /// Stores each slot payload in <see cref="PlayerPrefs"/> under a prefixed key.
    /// Simplest possible backend — always available on every platform (including WebGL)
    /// and requires no filesystem access, but not suited to large payloads. A companion
    /// index key tracks which slots exist so <see cref="List"/> can enumerate them.
    /// </summary>
    public class PlayerPrefsSaveProvider : ISaveProvider
    {
        private readonly string keyPrefix;
        private readonly string indexKey;

        public bool IsInitialized { get; private set; }

        public PlayerPrefsSaveProvider(string keyPrefix = "Kaddumi.Save.")
        {
            this.keyPrefix = string.IsNullOrEmpty(keyPrefix) ? "Kaddumi.Save." : keyPrefix;
            indexKey = this.keyPrefix + "__index";
        }

        public void Initialize(Action onComplete)
        {
            IsInitialized = true;
            Debug.Log("<color=cyan>[Save-PlayerPrefs]</color> Initialized");
            onComplete?.Invoke();
        }

        public void Write(string key, string data, Action<SaveResult> onComplete)
        {
            try
            {
                PlayerPrefs.SetString(FullKey(key), data);
                AddToIndex(key);
                PlayerPrefs.Save();
                onComplete?.Invoke(SaveResult.Ok());
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Io, e.Message));
            }
        }

        public void Read(string key, Action<SaveResult> onComplete)
        {
            string full = FullKey(key);
            if (!PlayerPrefs.HasKey(full))
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.NotFound, $"No save for key '{key}'."));
                return;
            }
            onComplete?.Invoke(SaveResult.Ok(PlayerPrefs.GetString(full)));
        }

        public void Delete(string key, Action<SaveResult> onComplete)
        {
            PlayerPrefs.DeleteKey(FullKey(key));
            RemoveFromIndex(key);
            PlayerPrefs.Save();
            onComplete?.Invoke(SaveResult.Ok());
        }

        public void Exists(string key, Action<bool> onComplete) =>
            onComplete?.Invoke(PlayerPrefs.HasKey(FullKey(key)));

        public void List(Action<string[]> onComplete) => onComplete?.Invoke(ReadIndex().ToArray());

        // --- Index bookkeeping ------------------------------------------------

        private string FullKey(string key) => keyPrefix + key;

        private List<string> ReadIndex()
        {
            string raw = PlayerPrefs.GetString(indexKey, string.Empty);
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var k in raw.Split('|'))
            {
                if (!string.IsNullOrEmpty(k)) list.Add(k);
            }
            return list;
        }

        private void WriteIndex(List<string> keys) =>
            PlayerPrefs.SetString(indexKey, string.Join("|", keys));

        private void AddToIndex(string key)
        {
            var keys = ReadIndex();
            if (!keys.Contains(key))
            {
                keys.Add(key);
                WriteIndex(keys);
            }
        }

        private void RemoveFromIndex(string key)
        {
            var keys = ReadIndex();
            if (keys.Remove(key)) WriteIndex(keys);
        }
    }
}
