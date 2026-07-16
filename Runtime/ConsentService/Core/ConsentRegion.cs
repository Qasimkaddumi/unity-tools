using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kaddumi.UnityTools.Consent
{
    /// <summary>
    /// Best-effort detection of whether the current user is likely in a region that
    /// requires GDPR-style consent (the EEA, the UK, plus Switzerland).
    ///
    /// This is a device-side heuristic based on the OS region / time zone. It is NOT a
    /// legally authoritative geo-lookup — when in doubt it errs on the side of asking for
    /// consent. For an authoritative signal, use the Google UMP provider (server-backed).
    /// </summary>
    public static class ConsentRegion
    {
        // EEA (EU 27 + Iceland, Liechtenstein, Norway) + United Kingdom + Switzerland.
        private static readonly HashSet<string> GdprCountryCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR",
            "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK",
            "SI", "ES", "SE", // EU 27
            "IS", "LI", "NO", // EEA extras
            "GB", "UK",       // United Kingdom
            "CH"              // Switzerland (FADP mirrors GDPR)
        };

        /// <summary>
        /// Returns true when the user is likely subject to GDPR-style consent rules.
        /// Falls back to <c>true</c> (ask for consent) if the region cannot be determined,
        /// so we never silently skip consent for someone who might need it.
        /// </summary>
        public static bool IsLikelyGdprRegion()
        {
            // 1. OS region (most reliable device-side signal).
            try
            {
                string country = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                if (!string.IsNullOrEmpty(country))
                    return GdprCountryCodes.Contains(country);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConsentRegion] Could not read OS region: {e.Message}. Falling back to time zone.");
            }

            // 2. Time-zone fallback (rough — European zones imply a GDPR region).
            try
            {
                string tz = TimeZoneInfo.Local.Id;
                if (!string.IsNullOrEmpty(tz) && tz.IndexOf("Europe", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConsentRegion] Could not read time zone: {e.Message}.");
            }

            // 3. Unknown → err on the privacy-protective side and ask for consent.
            return true;
        }
    }
}
