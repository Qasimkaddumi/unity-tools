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

        /// <summary>
        /// Id of the store this one mirrors, or null for an ordinary store. A mirror holds a
        /// copy of its primary's data: it receives the same saveables on every save, is
        /// skipped on load (the primary is authoritative, with the mirror as fallback when
        /// the primary can't be reached), and is reconciled by <c>SaveService.Sync</c>.
        ///
        /// <para>This is how a cloud store survives being offline: pair it with a local file
        /// mirror. Saves always land in the mirror, the cloud write is allowed to fail
        /// (<see cref="Required"/> off), and the next sync pushes whatever the cloud missed.</para>
        /// </summary>
        public string MirrorOf { get; }

        /// <summary>True when this store mirrors another rather than standing on its own.</summary>
        public bool IsMirror => MirrorOf != null;

        public SaveStore(string id, ISaveProvider provider, bool required = true,
            bool includeInAutoSave = true, string mirrorOf = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Store id must not be empty.", nameof(id));
            Id = id.Trim();
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Required = required;
            IncludeInAutoSave = includeInAutoSave;
            MirrorOf = string.IsNullOrWhiteSpace(mirrorOf) ? null : mirrorOf.Trim();

            if (MirrorOf != null && string.Equals(MirrorOf, Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Store '{Id}' cannot mirror itself.", nameof(mirrorOf));
            }
        }

        public override string ToString()
        {
            string notes = Required ? string.Empty : ", optional";
            if (IsMirror) notes += $", mirror of {MirrorOf}";
            return $"{Id} ({Provider.GetType().Name}{notes})";
        }
    }
}
