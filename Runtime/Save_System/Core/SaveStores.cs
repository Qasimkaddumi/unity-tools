namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Well-known save-store ids. Store ids are plain strings — define your own
    /// (<c>"profile"</c>, <c>"telemetry"</c>, ...) whenever these two aren't enough — but
    /// using these constants for the common local/cloud split keeps <see cref="ISaveable"/>
    /// implementations and the SaveManager inspector in agreement.
    /// </summary>
    public static class SaveStores
    {
        /// <summary>On-device storage: settings, cached state, anything that shouldn't need a network.</summary>
        public const string Local = "local";

        /// <summary>Remote storage: progression and anything that should follow the player across devices.</summary>
        public const string Cloud = "cloud";
    }
}
