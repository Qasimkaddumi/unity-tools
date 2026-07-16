using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Bookkeeping stored alongside every save. Surfaced by
    /// <see cref="SaveService.GetMetadata"/> so a "load game" screen can list slots
    /// (timestamp, playtime, build) without deserializing the whole payload.
    /// </summary>
    [Serializable]
    public class SaveMetadata
    {
        /// <summary>Slot this save belongs to.</summary>
        public int Slot;

        /// <summary>Save-format version at write time (see <c>SaveConfig.SaveVersion</c>).</summary>
        public int Version;

        /// <summary><c>Application.version</c> captured when the save was written.</summary>
        public string AppVersion;

        /// <summary>UTC write time in ISO-8601 ("o") format.</summary>
        public string SavedAtUtc;

        /// <summary>Accumulated play time in seconds, if the manager tracks it.</summary>
        public double PlaytimeSeconds;

        /// <summary>Parses <see cref="SavedAtUtc"/> back into a <see cref="DateTime"/> (UTC).</summary>
        public DateTime SavedAt()
        {
            if (DateTime.TryParse(SavedAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed;
            }
            return default;
        }

        public static SaveMetadata Create(int slot, int version, double playtimeSeconds) => new SaveMetadata
        {
            Slot = slot,
            Version = version,
            AppVersion = Application.version,
            SavedAtUtc = DateTime.UtcNow.ToString("o"),
            PlaytimeSeconds = playtimeSeconds
        };
    }
}
