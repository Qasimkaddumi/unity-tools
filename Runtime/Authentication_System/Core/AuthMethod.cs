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
        Google,         // Google / Google Play Games
        Apple,          // Sign in with Apple
        Facebook,       // Facebook Login
        Custom          // Custom token issued by your own backend
    }
}
