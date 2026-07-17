namespace Kaddumi.UnityTools.Auth.Core
{
    /// <summary>
    /// The various sign-in methods an <see cref="Interfaces.IAuthProvider"/> can support.
    /// A single provider may support several of these.
    /// </summary>
    public enum AuthMethod
    {
        Guest,          // Anonymous / device-based sign-in
        EmailPassword,  // Classic email + password
        Google,         // Google Sign-In
        Apple,          // Sign in with Apple
        Facebook,       // Facebook Login
        Custom,         // Custom token issued by your own backend

        // Native platform gamer identities. Unlike the OAuth methods above, the provider
        // performs the native sign-in itself, so no token has to be supplied by the caller.
        GooglePlayGames,// Google Play Games Services (Android gamer identity)
        GameCenter      // Apple Game Center (iOS / macOS / tvOS gamer identity)
    }
}
