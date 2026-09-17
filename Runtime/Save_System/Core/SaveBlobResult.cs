namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Outcome of a binary read. The byte-oriented sibling of <see cref="SaveResult"/>,
    /// used for blobs — screenshots, thumbnails, recordings — where routing the payload
    /// through a string would mean a base64 round-trip and roughly a third more bytes.
    /// </summary>
    public struct SaveBlobResult
    {
        public bool Success;

        /// <summary>Bytes returned by a successful read; null on failure.</summary>
        public byte[] Data;

        public SaveError Error;

        /// <summary>Size of the payload in bytes, or 0 when there is none.</summary>
        public int Length => Data?.Length ?? 0;

        public static SaveBlobResult Ok(byte[] data) => new SaveBlobResult
        {
            Success = true,
            Data = data
        };

        public static SaveBlobResult Fail(SaveError error) => new SaveBlobResult
        {
            Success = false,
            Error = error
        };

        public static SaveBlobResult Fail(SaveErrorType type, string message, int code = 0) =>
            Fail(new SaveError(type, message, code));
    }
}
