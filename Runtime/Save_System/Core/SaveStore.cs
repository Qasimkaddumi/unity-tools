using Kaddumi.UnityTools.Save.Interfaces;
using System;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// A named storage target: an <see cref="ISaveProvider"/> plus the policy that decides
    /// how the save system treats it. Each <see cref="ISaveable"/> names the store it
    /// belongs to via <see cref="ISaveable.StorageId"/>, so one <c>Save()</c> fans out into
    /// one payload per store — local settings to a file, progression to the cloud.
    /// </summary>
    public class SaveStore
    {
        /// <summary>Id saveables route to, e.g. <see cref="SaveStores.Local"/>. Case-insensitive, unique per service.</summary>
        public string Id { get; }

        public ISaveProvider Provider { get; }

        /// <summary>
        /// When true, a failure here fails the whole operation. Turn it off for a store that
        /// is allowed to be unavailable — a cloud store on a plane still shouldn't stop a
        /// local save from reporting success.
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// Whether periodic auto-saves include this store. Off is the usual choice for a
        /// cloud store, so a 60-second timer doesn't fire a network request every minute.
        /// </summary>
        public bool IncludeInAutoSave { get; }

        public SaveStore(string id, ISaveProvider provider, bool required = true, bool includeInAutoSave = true)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Store id must not be empty.", nameof(id));
            Id = id.Trim();
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Required = required;
            IncludeInAutoSave = includeInAutoSave;
        }

        public override string ToString() =>
            $"{Id} ({Provider.GetType().Name}{(Required ? string.Empty : ", optional")})";
    }
}
