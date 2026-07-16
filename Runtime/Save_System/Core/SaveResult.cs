namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Outcome of a save operation. On a successful read <see cref="Data"/> holds the
    /// raw stored payload; on failure <see cref="Error"/> describes what went wrong.
    /// Mirrors <c>AuthResult</c>.
    /// </summary>
    public struct SaveResult
    {
        public bool Success;

        /// <summary>Raw payload returned by a successful read; null for writes/deletes.</summary>
        public string Data;

        public SaveError Error;

        public static SaveResult Ok() => new SaveResult { Success = true };

        public static SaveResult Ok(string data) => new SaveResult
        {
            Success = true,
            Data = data
        };

        public static SaveResult Fail(SaveError error) => new SaveResult
        {
            Success = false,
            Error = error
        };

        public static SaveResult Fail(SaveErrorType type, string message, int code = 0) =>
            Fail(new SaveError(type, message, code));
    }
}
