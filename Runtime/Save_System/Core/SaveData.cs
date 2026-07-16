using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// The full contents of a single save slot: a set of per-<see cref="ISaveable"/>
    /// JSON blobs keyed by <c>SaveKey</c>, plus <see cref="SaveMetadata"/>.
    ///
    /// A flat list of entries is used instead of a <see cref="Dictionary{TKey,TValue}"/>
    /// because Unity's <see cref="JsonUtility"/> cannot serialize dictionaries. Each
    /// entry's <c>Json</c> is the saveable's own serialized state, so the container stays
    /// agnostic of any one system's data shape.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public SaveMetadata Metadata = new SaveMetadata();

        [SerializeField] private List<SaveEntry> entries = new List<SaveEntry>();

        /// <summary>All saveable keys present in this save.</summary>
        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var entry in entries) yield return entry.Key;
            }
        }

        public int Count => entries.Count;

        public bool Has(string key) => IndexOf(key) >= 0;

        /// <summary>Returns the stored JSON blob for <paramref name="key"/>, or null if absent.</summary>
        public string Get(string key)
        {
            int i = IndexOf(key);
            return i >= 0 ? entries[i].Json : null;
        }

        /// <summary>Inserts or replaces the JSON blob for <paramref name="key"/>.</summary>
        public void Set(string key, string json)
        {
            int i = IndexOf(key);
            if (i >= 0) entries[i] = new SaveEntry { Key = key, Json = json };
            else entries.Add(new SaveEntry { Key = key, Json = json });
        }

        public bool Remove(string key)
        {
            int i = IndexOf(key);
            if (i < 0) return false;
            entries.RemoveAt(i);
            return true;
        }

        public void Clear() => entries.Clear();

        private int IndexOf(string key)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == key) return i;
            }
            return -1;
        }

        [Serializable]
        public struct SaveEntry
        {
            public string Key;
            public string Json;
        }
    }
}
