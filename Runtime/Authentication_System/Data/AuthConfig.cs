using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Data
{
    /// <summary>
    /// Optional shared configuration for the authentication system. Holds the client IDs
    /// the native platform SDKs need before they can hand a token to a provider.
    /// Mirrors <c>AdConfig</c>: assign one asset in the AuthManager inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "AuthConfig", menuName = "Kaddumi/Auth/AuthConfig")]
    public class AuthConfig : ScriptableObject
    {
        [Header("Startup")]
        [Tooltip("Silently sign the user in as a Guest on startup if no session is restored.")]
        public bool AutoSignInGuest = false;

        [Header("Google")]
        [Tooltip("OAuth 2.0 Web Client ID from the Google/Firebase console (used to request an ID token).")]
        public string GoogleWebClientId;

        [Header("Facebook")]
        public string FacebookAppId;

        [Header("Apple")]
        [Tooltip("Service ID / bundle identifier configured for Sign in with Apple.")]
        public string AppleServiceId;
    }
}
