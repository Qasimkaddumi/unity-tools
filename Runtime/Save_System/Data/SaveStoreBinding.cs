using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Providers;
using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Data
{
    /// <summary>
    /// One row of the SaveManager's store list: the id saveables route to, the backend
    /// behind it, and how the save system should treat its failures. Turned into a runtime
    /// <see cref="SaveStore"/> at initialization.
    /// </summary>
    [Serializable]
    public class SaveStoreBinding
    {
        [Tooltip("Id saveables route to via ISaveable.StorageId — e.g. 'local' or 'cloud' " +
                 "(SaveStores.Local / SaveStores.Cloud). Must be unique in this list. The first " +
                 "row in the list is the default store for saveables that name none.")]
        public string Id = SaveStores.Local;

        [Tooltip("Storage backend for this store (PlayerPrefs, File, Encrypted File, or Cloud).")]
        public SaveProviderSO Provider;

        [Tooltip("Off means this store is allowed to fail without failing the whole save — the " +
                 "usual choice for a cloud store, so being offline doesn't make a good local " +
                 "save report as broken. Its failure still shows up in the SaveReport.")]
        public bool Required = true;

        [Tooltip("Whether the periodic auto-save writes this store. Usually off for a cloud " +
                 "store, so a 60-second timer doesn't fire a network request every minute.")]
        public bool IncludeInAutoSave = true;
    }
}
