using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Data;
using Kaddumi.UnityTools.Auth.Providers;
using Kaddumi.UnityTools.Services;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth
{
    /// <summary>
    /// MonoBehaviour host for the authentication system. Registers the provider assets
    /// selected in the inspector and exposes a small, SDK-agnostic API for game code.
    /// Structurally identical to <c>AnalyticsManager</c>: it owns an <see cref="AuthService"/>
    /// and initializes as an <see cref="IService"/>.
    /// </summary>
    public class AuthManager : MonoBehaviour, IService
    {
        public static AuthManager Instance { get; private set; }

        public AuthService Service { get; private set; }

        [Header("Configuration")]
        [Tooltip("Optional shared config (client IDs, auto guest sign-in).")]
        [SerializeField] private AuthConfig authConfig;

        [Tooltip("Assign provider ScriptableObjects (e.g. Firebase, Debug). Each request is routed to a provider that supports the requested method.")]
        [SerializeField] private List<AuthProviderSO> providers = new List<AuthProviderSO>();

        /// <summary>Raised whenever a user signs in (via any provider).</summary>
        public event Action<AuthUser> OnSignedIn;

        /// <summary>Raised when the shared session ends.</summary>
        public event Action OnSignedOut;

        public AuthUser CurrentUser => Service?.CurrentUser;
        public bool IsSignedIn => Service != null && Service.IsSignedIn;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Action onComplete)
        {
            Service = new AuthService();
            Service.OnSignedIn += user => OnSignedIn?.Invoke(user);
            Service.OnSignedOut += () => OnSignedOut?.Invoke();

            // Register the providers selected in the inspector.
            var selected = new List<AuthProviderSO>();
            foreach (var providerSO in providers)
            {
                if (providerSO != null) selected.Add(providerSO);
            }

            if (selected.Count == 0)
            {
                Debug.LogWarning("[AuthManager] No auth providers assigned in the inspector.");
                FinalizeInitialization(onComplete);
                return;
            }

            int remaining = selected.Count;
            foreach (var providerSO in selected)
            {
                Service.RegisterProvider(providerSO.CreateProvider(), () =>
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        FinalizeInitialization(onComplete);
                    }
                });
            }
        }

        private void FinalizeInitialization(Action onComplete)
        {
            // If a session was restored from a previous run, we are already signed in.
            if (!IsSignedIn && authConfig != null && authConfig.AutoSignInGuest)
            {
                SignInAsGuest(result =>
                {
                    if (!result.Success)
                    {
                        Debug.LogWarning($"[AuthManager] Auto guest sign-in failed: {result.Error}");
                    }
                    onComplete?.Invoke();
                });
                return;
            }

            onComplete?.Invoke();
        }

        // --- Public API -------------------------------------------------------

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete = null) =>
            Service.SignIn(credentials, WrapLog(onComplete));

        public void SignInAsGuest(Action<AuthResult> onComplete = null) =>
            SignIn(AuthCredentials.Guest(), onComplete);

        public void SignInWithEmail(string email, string password, Action<AuthResult> onComplete = null) =>
            SignIn(AuthCredentials.EmailPassword(email, password), onComplete);

        /// <summary>Sign in with a token already obtained from a native social SDK (Google/Apple/Facebook).</summary>
        public void SignInWithOAuth(AuthMethod method, string token, string secret = null, Action<AuthResult> onComplete = null) =>
            SignIn(AuthCredentials.OAuth(method, token, secret), onComplete);

        /// <summary>Sign in with Google Play Games (the provider runs the native Android flow).</summary>
        public void SignInWithGooglePlayGames(Action<AuthResult> onComplete = null) =>
            SignIn(AuthCredentials.GooglePlayGames(), onComplete);

        /// <summary>Sign in with Apple Game Center (the provider runs the native Apple flow).</summary>
        public void SignInWithGameCenter(Action<AuthResult> onComplete = null) =>
            SignIn(AuthCredentials.GameCenter(), onComplete);

        public void SignInWithCustomToken(string token, Action<AuthResult> onComplete = null) =>
            SignIn(AuthCredentials.Custom(token), onComplete);

        /// <summary>Attach another sign-in method to the current account (e.g. upgrade a guest).</summary>
        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete = null) =>
            Service.LinkMethod(credentials, WrapLog(onComplete));

        public void SignOut(Action onComplete = null) => Service.SignOut(onComplete);

        public void DeleteAccount(Action<AuthResult> onComplete = null) =>
            Service.DeleteAccount(WrapLog(onComplete));

        public bool IsMethodAvailable(AuthMethod method) => Service != null && Service.IsMethodAvailable(method);

        public IEnumerable<AuthMethod> AvailableMethods() =>
            Service != null ? Service.AvailableMethods() : new List<AuthMethod>();

        private static Action<AuthResult> WrapLog(Action<AuthResult> inner)
        {
            return result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"[AuthManager] Auth operation failed: {result.Error}");
                }
                inner?.Invoke(result);
            };
        }

        [ContextMenu("Test Guest Sign-In")]
        private void TestGuestSignIn()
        {
            SignInAsGuest(result => Debug.Log($"[AuthManager] Guest sign-in success={result.Success} user={result.User}"));
        }
    }
}
