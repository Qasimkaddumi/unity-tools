namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>High level classification of a save/load failure.</summary>
    public enum SaveErrorType
    {
        None,
        NotFound,             // No saved data exists for the requested slot/key
        Io,                   // Filesystem / PlayerPrefs read or write failed
        Serialization,        // Data could not be serialized or deserialized
        Corrupted,            // Stored data exists but is malformed / unreadable
        Encryption,           // Encrypt/decrypt failed (wrong key, tampered file)
        VersionMismatch,      // Stored data is from an incompatible save version
        ProviderNotAvailable, // No storage provider is registered
        NotInitialized,       // Operation attempted before the provider was ready
        InvalidSlot,          // Slot index outside the configured range
        Unknown
    }

    /// <summary>
    /// Lightweight, SDK-agnostic error container returned by save operations.
    /// Mirrors <c>AuthError</c> so failures can be surfaced uniformly.
    /// </summary>
    public struct SaveError
    {
        public SaveErrorType Type;
        public int Code;
        public string Message;

        public SaveError(SaveErrorType type, string message, int code = 0)
        {
            Type = type;
            Message = message;
            Code = code;
        }

        public override string ToString() => $"[{Type}] ({Code}) {Message}";
    }
}
