using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

#if PLAYFAB_SDK_INSTALLED
using PlayFab;
using PlayFab.ClientModels;
#endif

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Microsoft PlayFab Authentication backend. Compiles to a graceful no-op stub unless
    /// the <c>PLAYFAB_SDK_INSTALLED</c> scripting define is set in Player Settings, exactly
    /// like <see cref="FirebaseAuthProvider"/>. The PlayFab Title ID is read from the
    /// standard <c>PlayFabSharedSettings</c> asset configured by the PlayFab editor extension.
    ///
    /// Supported methods: Guest (persistent device custom ID), EmailPassword, Google, Apple,
    /// Facebook, Custom (arbitrary custom ID). Social flows expect an already-obtained token
    /// in <see cref="AuthCredentials.Token"/> (Google expects a server auth code).
    /// </summary>
    public class PlayFabAuthProvider : IAuthProvider
    {
        // PlayerPrefs key backing the anonymous/guest custom ID so a guest keeps the same
        // account across sessions on this device.
        private const string GuestIdKey = "kaddumi_playfab_guest_id";

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

#if PLAYFAB_SDK_INSTALLED
        // Remembers how the active session was created so a guest can be reported as such.
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
#if PLAYFAB_SDK_INSTALLED
            if (string.IsNullOrEmpty(PlayFabSettings.TitleId))
            {
                Debug.LogWarning("[Auth] PlayFab Title ID is not set. Configure it via Window > PlayFab > Editor Extensions.");
            }
            IsInitialized = true;
            Debug.Log("[Auth] PlayFab Auth Initialized Successfully");
            onComplete?.Invoke();
#else
            Debug.LogWarning("[Auth] PlayFab SDK not detected. Define 'PLAYFAB_SDK_INSTALLED' in Player Settings to enable.");
            onComplete?.Invoke();
#endif
        }

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if PLAYFAB_SDK_INSTALLED
            if (!IsInitialized)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.Unknown, "PlayFab Auth not initialized."));
                return;
            }

            var infoParams = new GetPlayerCombinedInfoRequestParams { GetUserAccountInfo = true };

            switch (credentials.Method)
            {
                case AuthMethod.Guest:
                    PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
                    {
                        CustomId = GetOrCreateGuestId(),
                        CreateAccount = true,
                        InfoRequestParameters = infoParams
                    }, r => OnLogin(r, AuthMethod.Guest, onComplete), e => OnError(e, onComplete));
                    break;

                case AuthMethod.Custom:
                    PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest
                    {
                        CustomId = credentials.Token,
                        CreateAccount = true,
                        InfoRequestParameters = infoParams
                    }, r => OnLogin(r, AuthMethod.Custom, onComplete), e => OnError(e, onComplete));
                    break;

                case AuthMethod.EmailPassword:
                    // Try to sign in first; register the account if it does not exist yet.
                    PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
                    {
                        Email = credentials.Email,
                        Password = credentials.Password,
                        InfoRequestParameters = infoParams
                    },
                    r => OnLogin(r, AuthMethod.EmailPassword, onComplete),
                    e =>
                    {
                        if (e.Error == PlayFabErrorCode.AccountNotFound)
                        {
                            RegisterEmail(credentials, onComplete);
                            return;
                        }
                        OnError(e, onComplete);
                    });
                    break;

                case AuthMethod.Google:
                    PlayFabClientAPI.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest
                    {
                        ServerAuthCode = credentials.Token,
                        CreateAccount = true,
                        InfoRequestParameters = infoParams
                    }, r => OnLogin(r, AuthMethod.Google, onComplete), e => OnError(e, onComplete));
                    break;

                case AuthMethod.Apple:
                    PlayFabClientAPI.LoginWithApple(new LoginWithAppleRequest
                    {
                        IdentityToken = credentials.Token,
                        CreateAccount = true,
                        InfoRequestParameters = infoParams
                    }, r => OnLogin(r, AuthMethod.Apple, onComplete), e => OnError(e, onComplete));
                    break;

                case AuthMethod.Facebook:
                    PlayFabClientAPI.LoginWithFacebook(new LoginWithFacebookRequest
                    {
                        AccessToken = credentials.Token,
                        CreateAccount = true,
                        InfoRequestParameters = infoParams
                    }, r => OnLogin(r, AuthMethod.Facebook, onComplete), e => OnError(e, onComplete));
                    break;

                default:
                    onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                        $"PlayFab does not support '{credentials.Method}'."));
                    break;
            }
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "PlayFab SDK not installed (PLAYFAB_SDK_INSTALLED not defined)."));
#endif
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
#if PLAYFAB_SDK_INSTALLED
            if (!PlayFabClientAPI.IsClientLoggedIn())
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn, "Not signed in."));
                return;
            }

            void OnLinked() => OnLinkSucceeded(credentials.Method, onComplete);

            switch (credentials.Method)
            {
                case AuthMethod.EmailPassword:
                    PlayFabClientAPI.AddUsernamePassword(new AddUsernamePasswordRequest
                    {
                        Email = credentials.Email,
                        Password = credentials.Password,
                        Username = credentials.DisplayName ?? credentials.Email
                    }, _ => OnLinked(), e => OnError(e, onComplete));
                    break;

                case AuthMethod.Google:
                    PlayFabClientAPI.LinkGoogleAccount(new LinkGoogleAccountRequest
                    {
                        ServerAuthCode = credentials.Token
                    }, _ => OnLinked(), e => OnError(e, onComplete));
                    break;

                case AuthMethod.Apple:
                    PlayFabClientAPI.LinkApple(new LinkAppleRequest
                    {
                        IdentityToken = credentials.Token
                    }, _ => OnLinked(), e => OnError(e, onComplete));
                    break;

                case AuthMethod.Facebook:
                    PlayFabClientAPI.LinkFacebookAccount(new LinkFacebookAccountRequest
                    {
                        AccessToken = credentials.Token
                    }, _ => OnLinked(), e => OnError(e, onComplete));
                    break;

                default:
                    onComplete?.Invoke(AuthResult.Fail(AuthErrorType.InvalidCredentials,
                        $"Cannot link '{credentials.Method}' with PlayFab."));
                    break;
            }
#else
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "PlayFab SDK not installed."));
#endif
        }

        public void SignOut(Action onComplete)
        {
#if PLAYFAB_SDK_INSTALLED
            PlayFabClientAPI.ForgetAllCredentials();
#endif
            CurrentUser = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke();
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
            // PlayFab does not expose account deletion through the client API; it must be
            // performed with the server/admin API or the "Delete master player account"
            // request from your own backend. We surface that clearly instead of pretending.
            onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                "PlayFab account deletion must be performed server-side (client API cannot delete accounts)."));
        }

#if PLAYFAB_SDK_INSTALLED
        private void RegisterEmail(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
            {
                Email = credentials.Email,
                Password = credentials.Password,
                Username = credentials.DisplayName,
                DisplayName = credentials.DisplayName,
                RequireBothUsernameAndEmail = false
            },
            register =>
            {
                // RegisterPlayFabUser signs the user in but returns a leaner result; build
                // the user from the register response directly.
                lastMethod = AuthMethod.EmailPassword;
                var user = new AuthUser(register.PlayFabId, AuthMethod.EmailPassword,
                    credentials.DisplayName, credentials.Email, false);
                CurrentUser = user;
                OnSignedIn?.Invoke(user);
                onComplete?.Invoke(AuthResult.Ok(user));
            },
            e => OnError(e, onComplete));
        }

        private void OnLogin(LoginResult result, AuthMethod method, Action<AuthResult> onComplete)
        {
            lastMethod = method;
            var user = Map(result, method);
            CurrentUser = user;
            OnSignedIn?.Invoke(user);
            onComplete?.Invoke(AuthResult.Ok(user));
        }

        private void OnLinkSucceeded(AuthMethod linked, Action<AuthResult> onComplete)
        {
            var methods = new List<AuthMethod> { lastMethod };
            if (linked != lastMethod) methods.Add(linked);

            string id = CurrentUser?.UserId;
            var user = new AuthUser(id, methods, CurrentUser?.DisplayName, CurrentUser?.Email,
                lastMethod == AuthMethod.Guest);
            CurrentUser = user;
            onComplete?.Invoke(AuthResult.Ok(user));
        }

        private void OnError(PlayFabError error, Action<AuthResult> onComplete)
        {
            onComplete?.Invoke(AuthResult.Fail(ToError(error)));
        }

        private static AuthUser Map(LoginResult result, AuthMethod method)
        {
            bool isGuest = method == AuthMethod.Guest;

            string displayName = null;
            string email = null;
            var account = result.InfoResultPayload?.AccountInfo;
            if (account != null)
            {
                displayName = account.TitleInfo?.DisplayName;
                email = account.PrivateInfo?.Email;
            }

            return new AuthUser(result.PlayFabId, method, displayName, email, isGuest);
        }

        private static string GetOrCreateGuestId()
        {
            var id = PlayerPrefs.GetString(GuestIdKey, null);
            if (string.IsNullOrEmpty(id))
            {
                id = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
                {
                    id = Guid.NewGuid().ToString("N");
                }
                PlayerPrefs.SetString(GuestIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        private static AuthError ToError(PlayFabError error)
        {
            var type = AuthErrorType.Unknown;
            switch (error.Error)
            {
                case PlayFabErrorCode.ConnectionError:
                case PlayFabErrorCode.ServiceUnavailable:
                    type = AuthErrorType.Network;
                    break;
                case PlayFabErrorCode.InvalidEmailOrPassword:
                case PlayFabErrorCode.InvalidUsernameOrPassword:
                case PlayFabErrorCode.InvalidParams:
                    type = AuthErrorType.InvalidCredentials;
                    break;
                case PlayFabErrorCode.AccountNotFound:
                    type = AuthErrorType.UserNotFound;
                    break;
                case PlayFabErrorCode.EmailAddressNotAvailable:
                case PlayFabErrorCode.UsernameNotAvailable:
                    type = AuthErrorType.EmailAlreadyInUse;
                    break;
            }

            return new AuthError(type, error.ErrorMessage ?? "Unknown PlayFab error.", (int)error.Error);
        }
#endif
    }
}
