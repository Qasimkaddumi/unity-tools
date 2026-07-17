using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using UnityEngine;

#if GAME_CENTER_SDK_INSTALLED
using UnityEngine.SocialPlatforms;
#endif

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Apple Game Center backend for the iOS / macOS / tvOS gamer identity. Compiles to a
    /// graceful no-op stub unless the <c>GAME_CENTER_SDK_INSTALLED</c> scripting define is set
    /// in Player Settings, mirroring the other auth providers. No package is required — this
    /// uses Unity's built-in <see cref="UnityEngine.Social"/> API (backed by Game Center on
    /// Apple platforms); just add the define on your Apple build targets to enable it.
    ///
    /// <para>Supported method: <see cref="AuthMethod.GameCenter"/> only. The provider runs the
    /// native GameKit sign-in itself and maps the authenticated local player to an
    /// <see cref="AuthUser"/>, so callers pass no token.</para>
    /// </summary>
    public class GameCenterAuthProvider : IAuthProvider
    {
        public bool IsInitialized { get; private set; }
        public AuthUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;

        public event Action<AuthUser> OnSignedIn;
        public event Action OnSignedOut;

        public bool SupportsMethod(AuthMethod method) => method == AuthMethod.GameCenter;

        public void Initialize(Action onComplete)
        {
#if GAME_CENTER_SDK_INSTALLED
            IsInitialized = true;
            Debug.Log("[Auth] Game Center Initialized Successfully");
            onComplete?.Invoke();
#else
            Debug.LogWarning("[Auth] Game Center not enabled. Define 'GAME_CENTER_SDK_INSTALLED' in Player Settings (Apple platforms) to enable.");
            onComplete?.Invoke();
#endif
        }

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if GAME_CENTER_SDK_INSTALLED
            if (!IsInitialized)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.Unknown, "Game Center not initialized."));
                return;
            }

            if (credentials.Method != AuthMethod.GameCenter)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                    $"Game Center does not support '{credentials.Method}'."));
                return;
            }

            Social.localUser.Authenticate((bool success, string error) =>
            {
                if (!success)
                {
                    onComplete?.Invoke(AuthResult.Fail(AuthErrorType.InvalidCredentials,
                        string.IsNullOrEmpty(error) ? "Game Center authentication was declined." : error));
                    return;
                }

                var local = Social.localUser;
                var user = new AuthUser(local.id, AuthMethod.GameCenter,
                    string.IsNullOrEmpty(local.userName) ? null : local.userName, null, false);

                CurrentUser = user;
                OnSignedIn?.Invoke(user);
                onComplete?.Invoke(AuthResult.Ok(user));
            });
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Game Center not enabled (GAME_CENTER_SDK_INSTALLED not defined)."));
#endif
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            // Game Center is a self-contained identity managed by iOS; it cannot host other
            // linked methods. Federate it into a backend if you need account linking.
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Game Center cannot link additional methods."));
        }

        public void SignOut(Action onComplete)
        {
            // Game Center has no client-side sign-out; the session is owned by the OS. We drop
            // the local reference so game code reflects a signed-out state.
            CurrentUser = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke();
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Game Center accounts are managed by Apple and cannot be deleted from the game."));
        }
    }
}
