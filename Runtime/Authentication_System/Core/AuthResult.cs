namespace Kaddumi.UnityTools.Auth.Core
{
    /// <summary>
    /// Outcome of an authentication operation. On success <see cref="User"/> is populated;
    /// on failure <see cref="Error"/> describes what went wrong.
    /// </summary>
    public struct AuthResult
    {
        public bool Success;
        public AuthUser User;
        public AuthError Error;

        public static AuthResult Ok(AuthUser user) => new AuthResult
        {
            Success = true,
            User = user
        };

        public static AuthResult Fail(AuthError error) => new AuthResult
        {
            Success = false,
            Error = error
        };

        public static AuthResult Fail(AuthErrorType type, string message, int code = 0) =>
            Fail(new AuthError(type, message, code));
    }
}
