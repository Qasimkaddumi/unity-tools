namespace Kaddumi.UnityTools.Consent
{
    /// <summary>
    /// Resolved privacy-consent state for the current user.
    /// </summary>
    public enum ConsentStatus
    {
        /// <summary>No decision has been made yet (first launch / pending form).</summary>
        Unknown = 0,

        /// <summary>User accepted (or consent is not required in their region).</summary>
        Granted = 1,

        /// <summary>User rejected.</summary>
        Denied = 2
    }
}
