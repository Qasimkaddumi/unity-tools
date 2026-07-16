using UnityEngine;

namespace Kaddumi.UnityTools.Consent
{
    /// <summary>
    /// Persists the user's consent decision across sessions using <see cref="PlayerPrefs"/>.
    ///
    /// A <c>version</c> is stored alongside the choice so you can invalidate previously
    /// gathered consent (and re-prompt everyone) when your privacy policy or the set of
    /// vendors/purposes changes — simply bump the version on the provider.
    /// </summary>
    public static class ConsentStorage
    {
        private const string StatusKey = "Kaddumi.Consent.Status";
        private const string VersionKey = "Kaddumi.Consent.Version";
        private const string TimestampKey = "Kaddumi.Consent.Timestamp";

        /// <summary>
        /// True when a decision has been stored for the given <paramref name="version"/>.
        /// A mismatching version is treated as "no stored consent" so the user is asked again.
        /// </summary>
        public static bool HasStoredConsent(int version)
        {
            if (!PlayerPrefs.HasKey(StatusKey)) return false;
            return PlayerPrefs.GetInt(VersionKey, -1) == version;
        }

        /// <summary>Returns the stored status, or <see cref="ConsentStatus.Unknown"/> if none.</summary>
        public static ConsentStatus Load()
        {
            int raw = PlayerPrefs.GetInt(StatusKey, (int)ConsentStatus.Unknown);
            return (ConsentStatus)raw;
        }

        /// <summary>Persists a decision together with the provider's consent version and a UTC timestamp.</summary>
        public static void Save(ConsentStatus status, int version)
        {
            PlayerPrefs.SetInt(StatusKey, (int)status);
            PlayerPrefs.SetInt(VersionKey, version);
            PlayerPrefs.SetString(TimestampKey, System.DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
        }

        /// <summary>Removes all stored consent data (used by reset / right-to-erasure flows).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(StatusKey);
            PlayerPrefs.DeleteKey(VersionKey);
            PlayerPrefs.DeleteKey(TimestampKey);
            PlayerPrefs.Save();
        }
    }
}
