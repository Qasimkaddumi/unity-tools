using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_AUTHENTICATION_SDK_INSTALLED
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
#endif

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Unity Gaming Services (UGS) Authentication backend. Compiles to a graceful no-op
    /// stub unless the <c>UNITY_AUTHENTICATION_SDK_INSTALLED</c> scripting define is set in
    /// Player Settings, exactly like <see cref="FirebaseAuthProvider"/>.
    ///
    /// Supported methods: Guest (anonymous), EmailPassword (username/password), Google,
    /// Apple, Facebook. The social flows expect an already-obtained OAuth token in
    /// <see cref="AuthCredentials.Token"/> (fetch it with the platform's native sign-in SDK).
    /// Custom tokens are not part of UGS and are reported as unsupported.
    /// </summary>
    public class UnityAuthProvider : IAuthProvider
    {
        private static readonly AuthMethod[] Supported =
        {
            AuthMethod.Guest,
            AuthMethod.EmailPassword,
            AuthMethod.Google,
            AuthMethod.Apple,
            AuthMethod.Facebook
        };

        public bool IsInitialized { get; private set; }
        public AuthUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;

        public event Action<AuthUser> OnSignedIn;
        public event Action OnSignedOut;

#if UNITY_AUTHENTICATION_SDK_INSTALLED
        // Remembers how the active session was created so an anonymous player can be
        // reported as a Guest (UGS does not expose this directly).
        private AuthMethod lastMethod = AuthMethod.Guest;
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
#if UNITY_AUTHENTICATION_SDK_INSTALLED
            InitializeAsync(onComplete);
#else
            Debug.LogWarning("[Auth] Unity Authentication SDK not detected. Define 'UNITY_AUTHENTICATION_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if UNITY_AUTHENTICATION_SDK_INSTALLED
            if (!IsInitialized)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.Unknown, "Unity Authentication not initialized."));
                return;
            }
            SignInAsync(credentials, onComplete);
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Unity Authentication SDK not installed (UNITY_AUTHENTICATION_SDK_INSTALLED not defined)."));
#endif
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if UNITY_AUTHENTICATION_SDK_INSTALLED
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn, "Not signed in."));
                return;
            }
            LinkAsync(credentials, onComplete);
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Unity Authentication SDK not installed."));
#endif
        }

        public void SignOut(Action onComplete)
        {
#if UNITY_AUTHENTICATION_SDK_INSTALLED
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }
#endif
            CurrentUser = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke();
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
#if UNITY_AUTHENTICATION_SDK_INSTALLED
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn, "Not signed in."));
                return;
            }
            DeleteAccountAsync(onComplete);
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "Unity Authentication SDK not installed."));
#endif
        }

#if UNITY_AUTHENTICATION_SDK_INSTALLED
        private async void InitializeAsync(Action onComplete)
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                AuthenticationService.Instance.SignedOut += HandleSignedOut;

                // Restore any cached session from a previous run.
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    CurrentUser = Map();
                }

                IsInitialized = true;
                Debug.Log("[Auth] Unity Authentication Initialized Successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Auth] Could not initialize Unity Authentication: {e.Message}");
            }
            finally
            {
                onComplete?.Invoke();
            }
        }

        private async void SignInAsync(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            try
            {
                switch (credentials.Method)
                {
                    case AuthMethod.Guest:
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                        break;

                    case AuthMethod.EmailPassword:
                        // Register on first use, otherwise fall back to sign-in.
                        try
                        {
                            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(credentials.Email, credentials.Password);
                        }
                        catch (AuthenticationException)
                        {
                            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(credentials.Email, credentials.Password);
                        }
                        break;

                    case AuthMethod.Google:
                        await AuthenticationService.Instance.SignInWithGoogleAsync(credentials.Token);
                        break;

                    case AuthMethod.Apple:
                        await AuthenticationService.Instance.SignInWithAppleAsync(credentials.Token);
                        break;

                    case AuthMethod.Facebook:
                        await AuthenticationService.Instance.SignInWithFacebookAsync(credentials.Token);
                        break;

                    default:
                        onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                            $"Unity Authentication does not support '{credentials.Method}'."));
                        return;
                }

                Complete(credentials.Method, onComplete);
            }
            catch (Exception e)
            {
                onComplete?.Invoke(AuthResult.Fail(ToError(e)));
            }
        }

        private async void LinkAsync(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            try
            {
                switch (credentials.Method)
                {
                    case AuthMethod.EmailPassword:
                        await AuthenticationService.Instance.AddUsernamePasswordAsync(credentials.Email, credentials.Password);
                        break;
                    case AuthMethod.Google:
                        await AuthenticationService.Instance.LinkWithGoogleAsync(credentials.Token);
                        break;
                    case AuthMethod.Apple:
                        await AuthenticationService.Instance.LinkWithAppleAsync(credentials.Token);
                        break;
                    case AuthMethod.Facebook:
                        await AuthenticationService.Instance.LinkWithFacebookAsync(credentials.Token);
                        break;
                    default:
                        onComplete?.Invoke(AuthResult.Fail(AuthErrorType.InvalidCredentials,
                            $"Cannot link '{credentials.Method}' with Unity Authentication."));
                        return;
                }

                // Preserve the original session method; a linked method is additive.
                var user = Map(lastMethod, credentials.Method);
                CurrentUser = user;
                onComplete?.Invoke(AuthResult.Ok(user));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(AuthResult.Fail(ToError(e)));
            }
        }

        private async void DeleteAccountAsync(Action<AuthResult> onComplete)
        {
            try
            {
                await AuthenticationService.Instance.DeleteAccountAsync();
                CurrentUser = null;
                OnSignedOut?.Invoke();
                onComplete?.Invoke(AuthResult.Ok(null));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(AuthResult.Fail(ToError(e)));
            }
        }

        private void Complete(AuthMethod method, Action<AuthResult> onComplete)
        {
            lastMethod = method;
            var user = Map(method);
            CurrentUser = user;
            OnSignedIn?.Invoke(user);
            onComplete?.Invoke(AuthResult.Ok(user));
        }

        private void HandleSignedOut()
        {
            if (CurrentUser != null)
            {
                CurrentUser = null;
                OnSignedOut?.Invoke();
            }
        }

        private AuthUser Map(params AuthMethod[] extraMethods)
        {
            var instance = AuthenticationService.Instance;
            bool isGuest = lastMethod == AuthMethod.Guest;

            var methods = new List<AuthMethod> { lastMethod };
            foreach (var extra in extraMethods)
            {
                if (extra != lastMethod && !methods.Contains(extra)) methods.Add(extra);
            }

            string name = string.IsNullOrEmpty(instance.PlayerName) ? null : instance.PlayerName;
            return new AuthUser(instance.PlayerId, methods, name, null, isGuest);
        }

        private static AuthError ToError(Exception exception)
        {
            var type = AuthErrorType.Unknown;
            int code = 0;

            if (exception is AuthenticationException authEx)
            {
                code = authEx.ErrorCode;
                switch (authEx.ErrorCode)
                {
                    case AuthenticationErrorCodes.InvalidParameters:
                        type = AuthErrorType.InvalidCredentials;
                        break;
                    case AuthenticationErrorCodes.AccountAlreadyLinked:
                    case AuthenticationErrorCodes.AccountLinkLimitExceeded:
                        type = AuthErrorType.EmailAlreadyInUse;
                        break;
                }
            }
            else if (exception is RequestFailedException reqEx)
            {
                code = reqEx.ErrorCode;
                type = AuthErrorType.Network;
            }

            return new AuthError(type, exception?.Message ?? "Unknown Unity Authentication error.", code);
        }
#endif
    }
}
