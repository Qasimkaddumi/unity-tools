namespace Kaddumi.UnityTools.Auth.Core
{
    /// <summary>
    /// A single, method-agnostic payload passed to a provider when signing in or linking.
    /// Only the fields relevant to <see cref="Method"/> need to be populated; use the
    /// static factory helpers below rather than filling this in by hand.
    /// </summary>
    public struct AuthCredentials
    {
        public AuthMethod Method;

        // Email / password flow
        public string Email;
        public string Password;

        // OAuth / social / custom flow
        public string Token;        // ID token or access token from the platform SDK
        public string Secret;       // Optional secondary token (e.g. Apple / Twitter)

        public string DisplayName;  // Optional profile name to attach on creation

        /// <summary>Anonymous, device-scoped sign-in.</summary>
        public static AuthCredentials Guest() => new AuthCredentials
        {
            Method = AuthMethod.Guest
        };

        /// <summary>Email + password sign-in or registration.</summary>
        public static AuthCredentials EmailPassword(string email, string password, string displayName = null) => new AuthCredentials
        {
            Method = AuthMethod.EmailPassword,
            Email = email,
            Password = password,
            DisplayName = displayName
        };

        /// <summary>Social sign-in (Google / Apple / Facebook) using a token obtained from the platform SDK.</summary>
        public static AuthCredentials OAuth(AuthMethod method, string token, string secret = null) => new AuthCredentials
        {
            Method = method,
            Token = token,
            Secret = secret
        };

        /// <summary>Sign-in with a custom token minted by your own backend.</summary>
        public static AuthCredentials Custom(string token) => new AuthCredentials
        {
            Method = AuthMethod.Custom,
            Token = token
        };
    }
}
