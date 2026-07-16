namespace Kaddumi.UnityTools.Auth.Core
{
    /// <summary>High level classification of an authentication failure.</summary>
    public enum AuthErrorType
    {
        None,
        Cancelled,            // User dismissed the sign-in flow
        Network,              // No connectivity / timeout
        InvalidCredentials,   // Wrong email/password or bad token
        UserNotFound,
        EmailAlreadyInUse,
        ProviderNotAvailable, // No registered provider supports the method
        NotSignedIn,          // Operation requires an active session
        Unknown
    }

    /// <summary>
    /// Lightweight, SDK-agnostic error container returned by auth operations.
    /// Mirrors <c>AdErrorDomain</c> so failures can be surfaced uniformly.
    /// </summary>
    public struct AuthError
    {
        public AuthErrorType Type;
        public int Code;
        public string Message;

        public AuthError(AuthErrorType type, string message, int code = 0)
        {
            Type = type;
            Message = message;
            Code = code;
        }

        public override string ToString() => $"[{Type}] ({Code}) {Message}";
    }
}
