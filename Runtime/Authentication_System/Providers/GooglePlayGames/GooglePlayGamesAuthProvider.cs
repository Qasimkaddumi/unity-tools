using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using UnityEngine;

#if GOOGLE_PLAY_GAMES_SDK_INSTALLED
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Google Play Games Services (v2) backend for the Android gamer identity. Compiles to a
    /// graceful no-op stub unless the <c>GOOGLE_PLAY_GAMES_SDK_INSTALLED</c> scripting define
    /// is set in Player Settings, exactly like <see cref="FirebaseAuthProvider"/>. Install the
    /// <c>com.google.play.games</c> plugin and add the define to enable it.
    ///
    /// <para>Supported method: <see cref="AuthMethod.GooglePlayGames"/> only. The provider runs
    /// the native Play Games sign-in itself and maps the local player to an <see cref="AuthUser"/>,
    /// so callers pass no token. When <see cref="requestServerAuthCode"/> is enabled it also
    /// requests a one-time server-side auth code, exposed via <see cref="ServerAuthCode"/> for
    /// bridging into a backend (e.g. hand it to Firebase/PlayFab/Unity as an OAuth credential).</para>
    /// </summary>
    public class GooglePlayGamesAuthProvider : IAuthProvider
    {
        private readonly bool requestServerAuthCode;

        /// <summary>
        /// The most recent Play Games server-side auth code, or null. Only populated when the
        /// provider is created with server auth code requests enabled and sign-in succeeds.
        /// Use it to federate the Play Games identity into a backend of your choice.
        /// </summary>
        public string ServerAuthCode { get; private set; }

        public bool IsInitialized { get; private set; }
        public AuthUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;

        public event Action<AuthUser> OnSignedIn;
        public event Action OnSignedOut;

        public GooglePlayGamesAuthProvider(bool requestServerAuthCode = false)
        {
            this.requestServerAuthCode = requestServerAuthCode;
        }

        public bool SupportsMethod(AuthMethod method) => method == AuthMethod.GooglePlayGames;

        public void Initialize(Action onComplete)
        {
#if GOOGLE_PLAY_GAMES_SDK_INSTALLED
            PlayGamesPlatform.Activate();
            IsInitialized = true;
            Debug.Log("[Auth] Google Play Games Initialized Successfully");
            onComplete?.Invoke();
#else
            Debug.LogWarning("[Auth] Google Play Games SDK not detected. Install 'com.google.play.games' and define 'GOOGLE_PLAY_GAMES_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if GOOGLE_PLAY_GAMES_SDK_INSTALLED
            if (!IsInitialized)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.Unknown, "Google Play Games not initialized."));
                return;
            }

            if (credentials.Method != AuthMethod.GooglePlayGames)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                    $"Google Play Games does not support '{credentials.Method}'."));
                return;
            }

            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success)
                {
                    onComplete?.Invoke(AuthResult.Fail(ToError(status)));
                    return;
                }

                if (requestServerAuthCode)
                {
                    // forceRefreshToken:false reuses a cached grant when possible.
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                    {
                        ServerAuthCode = code;
                        Complete(onComplete);
                    });
                    return;
                }

                Complete(onComplete);
            });
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Google Play Games SDK not installed (GOOGLE_PLAY_GAMES_SDK_INSTALLED not defined)."));
#endif
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            // Play Games is a self-contained identity; linking other methods onto it is handled
            // by federating its server auth code into a backend, not by this provider.
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Google Play Games cannot link additional methods. Federate ServerAuthCode into a backend instead."));
        }

        public void SignOut(Action onComplete)
        {
            // Play Games v2 has no client-side sign-out; the session is managed by the OS.
            // We simply drop the local reference so game code reflects a signed-out state.
            CurrentUser = null;
            ServerAuthCode = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke();
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Google Play Games accounts are managed by Google and cannot be deleted from the game."));
        }

#if GOOGLE_PLAY_GAMES_SDK_INSTALLED
        private void Complete(Action<AuthResult> onComplete)
        {
            var instance = PlayGamesPlatform.Instance;
            string id = instance.GetUserId();
            string name = instance.GetUserDisplayName();

            var user = new AuthUser(id, AuthMethod.GooglePlayGames,
                string.IsNullOrEmpty(name) ? null : name, null, false);

            CurrentUser = user;
            OnSignedIn?.Invoke(user);
            onComplete?.Invoke(AuthResult.Ok(user));
        }

        private static AuthError ToError(SignInStatus status)
        {
            var type = status == SignInStatus.Canceled ? AuthErrorType.Cancelled : AuthErrorType.Unknown;
            return new AuthError(type, $"Google Play Games sign-in failed: {status}.", (int)status);
        }
#endif
    }
}
