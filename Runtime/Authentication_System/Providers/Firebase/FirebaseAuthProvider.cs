using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

#if FIREBASE_AUTH_SDK_INSTALLED
using Firebase;
using Firebase.Extensions;
// Alias the Firebase.Auth namespace so its AuthResult / AuthError types never clash
// with our own Core.AuthResult / Core.AuthError.
using FB = Firebase.Auth;
#endif

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Firebase Authentication backend. Compiles to a graceful no-op stub unless the
    /// <c>FIREBASE_AUTH_SDK_INSTALLED</c> scripting define is set in Player Settings,
    /// exactly like the Firebase analytics/ads providers.
    ///
    /// Supported methods: Guest (anonymous), EmailPassword, Google, Apple, Facebook, Custom.
    /// The social flows expect an already-obtained OAuth token in
    /// <see cref="AuthCredentials.Token"/> (fetch it with the platform's native sign-in SDK).
    /// </summary>
    public class FirebaseAuthProvider : IAuthProvider
    {
        private static readonly AuthMethod[] Supported =
        {
            AuthMethod.Guest,
            AuthMethod.EmailPassword,
            AuthMethod.Google,
            AuthMethod.Apple,
            AuthMethod.Facebook,
            AuthMethod.Custom
        };

        public bool IsInitialized { get; private set; }
        public AuthUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;

        public event Action<AuthUser> OnSignedIn;
        public event Action OnSignedOut;

#if FIREBASE_AUTH_SDK_INSTALLED
        private FB.FirebaseAuth auth;
#endif

        public bool SupportsMethod(AuthMethod method)
        {
            foreach (var m in Supported)
            {
                if (m == method) return true;
            }
            return false;
        }

        public void Initialize(Action onComplete)
        {
#if FIREBASE_AUTH_SDK_INSTALLED
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogError($"[Auth] Could not resolve Firebase dependencies: {task.Result}");
                    onComplete?.Invoke();
                    return;
                }

                auth = FB.FirebaseAuth.DefaultInstance;
                auth.StateChanged += HandleAuthStateChanged;

                // Restore any cached session from a previous run.
                if (auth.CurrentUser != null)
                {
                    CurrentUser = Map(auth.CurrentUser);
                }

                IsInitialized = true;
                Debug.Log("[Auth] Firebase Auth Initialized Successfully");
                onComplete?.Invoke();
            });
#else
            Debug.LogWarning("[Auth] Firebase Auth SDK not detected. Define 'FIREBASE_AUTH_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if FIREBASE_AUTH_SDK_INSTALLED
            if (!IsInitialized || auth == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.Unknown, "Firebase Auth not initialized."));
                return;
            }

            switch (credentials.Method)
            {
                case AuthMethod.Guest:
                    HandleTask(auth.SignInAnonymouslyAsync(), onComplete);
                    break;

                case AuthMethod.EmailPassword:
                    // Create the account on first use, otherwise fall back to sign-in.
                    auth.CreateUserWithEmailAndPasswordAsync(credentials.Email, credentials.Password)
                        .ContinueWithOnMainThread(create =>
                        {
                            if (create.Exception == null)
                            {
                                Complete(create.Result?.User, onComplete);
                                return;
                            }
                            HandleTask(auth.SignInWithEmailAndPasswordAsync(credentials.Email, credentials.Password), onComplete);
                        });
                    break;

                case AuthMethod.Custom:
                    HandleTask(auth.SignInWithCustomTokenAsync(credentials.Token), onComplete);
                    break;

                default:
                    FB.Credential credential = BuildCredential(credentials);
                    if (credential == null)
                    {
                        onComplete?.Invoke(AuthResult.Fail(AuthErrorType.InvalidCredentials,
                            $"Missing token for {credentials.Method} sign-in."));
                        return;
                    }
                    HandleTask(auth.SignInWithCredentialAsync(credential), onComplete);
                    break;
            }
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Firebase Auth SDK not installed (FIREBASE_AUTH_SDK_INSTALLED not defined)."));
#endif
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if FIREBASE_AUTH_SDK_INSTALLED
            var user = auth?.CurrentUser;
            if (user == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn, "Not signed in."));
                return;
            }

            FB.Credential credential = BuildCredential(credentials);
            if (credential == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.InvalidCredentials,
                    $"Cannot build credential for {credentials.Method}."));
                return;
            }

            HandleTask(user.LinkWithCredentialAsync(credential), onComplete);
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Firebase Auth SDK not installed."));
#endif
        }

        public void SignOut(Action onComplete)
        {
#if FIREBASE_AUTH_SDK_INSTALLED
            auth?.SignOut();
#endif
            CurrentUser = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke();
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
#if FIREBASE_AUTH_SDK_INSTALLED
            var user = auth?.CurrentUser;
            if (user == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn, "Not signed in."));
                return;
            }

            user.DeleteAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Exception != null)
                {
                    onComplete?.Invoke(AuthResult.Fail(ToError(task.Exception)));
                    return;
                }

                CurrentUser = null;
                OnSignedOut?.Invoke();
                onComplete?.Invoke(AuthResult.Ok(null));
            });
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Firebase Auth SDK not installed."));
#endif
        }

#if FIREBASE_AUTH_SDK_INSTALLED
        private static FB.Credential BuildCredential(AuthCredentials c)
        {
            switch (c.Method)
            {
                case AuthMethod.Google:
                    return FB.GoogleAuthProvider.GetCredential(c.Token, c.Secret);
                case AuthMethod.Apple:
                    // Apple is exposed as a generic OAuth provider; token is the identity token.
                    return FB.OAuthProvider.GetCredential("apple.com", c.Token, c.Secret, null);
                case AuthMethod.Facebook:
                    return FB.FacebookAuthProvider.GetCredential(c.Token);
                default:
                    return null;
            }
        }

        private void HandleTask(System.Threading.Tasks.Task<FB.AuthResult> task, Action<AuthResult> onComplete)
        {
            task.ContinueWithOnMainThread(t =>
            {
                if (t.Exception != null)
                {
                    onComplete?.Invoke(AuthResult.Fail(ToError(t.Exception)));
                    return;
                }
                Complete(t.Result?.User, onComplete);
            });
        }

        private void Complete(FB.FirebaseUser fbUser, Action<AuthResult> onComplete)
        {
            if (fbUser == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.Unknown, "Sign-in returned no user."));
                return;
            }

            var user = Map(fbUser);
            CurrentUser = user;
            OnSignedIn?.Invoke(user);
            onComplete?.Invoke(AuthResult.Ok(user));
        }

        private void HandleAuthStateChanged(object sender, EventArgs e)
        {
            var fbUser = auth.CurrentUser;
            if (fbUser == null && CurrentUser != null)
            {
                CurrentUser = null;
                OnSignedOut?.Invoke();
            }
            else if (fbUser != null)
            {
                CurrentUser = Map(fbUser);
            }
        }

        private static AuthUser Map(FB.FirebaseUser fbUser)
        {
            var methods = new List<AuthMethod>();
            foreach (var info in fbUser.ProviderData)
            {
                methods.Add(MapProviderId(info.ProviderId));
            }
            if (methods.Count == 0) methods.Add(fbUser.IsAnonymous ? AuthMethod.Guest : AuthMethod.Custom);

            return new AuthUser(
                fbUser.UserId,
                methods,
                string.IsNullOrEmpty(fbUser.DisplayName) ? null : fbUser.DisplayName,
                string.IsNullOrEmpty(fbUser.Email) ? null : fbUser.Email,
                fbUser.IsAnonymous);
        }

        private static AuthMethod MapProviderId(string providerId)
        {
            switch (providerId)
            {
                case "password": return AuthMethod.EmailPassword;
                case "google.com": return AuthMethod.Google;
                case "apple.com": return AuthMethod.Apple;
                case "facebook.com": return AuthMethod.Facebook;
                default: return AuthMethod.Custom;
            }
        }

        private static AuthError ToError(AggregateException exception)
        {
            var inner = exception?.Flatten().InnerException;
            var baseEx = inner as FirebaseException;

            var type = AuthErrorType.Unknown;
            int code = baseEx?.ErrorCode ?? 0;

            if (baseEx != null)
            {
                switch ((FB.AuthError)code)
                {
                    case FB.AuthError.NetworkRequestFailed:
                        type = AuthErrorType.Network;
                        break;
                    case FB.AuthError.WrongPassword:
                    case FB.AuthError.InvalidCredential:
                        type = AuthErrorType.InvalidCredentials;
                        break;
                    case FB.AuthError.UserNotFound:
                        type = AuthErrorType.UserNotFound;
                        break;
                    case FB.AuthError.EmailAlreadyInUse:
                        type = AuthErrorType.EmailAlreadyInUse;
                        break;
                }
            }

            return new AuthError(type, inner?.Message ?? "Unknown Firebase auth error.", code);
        }
#endif
    }
}
