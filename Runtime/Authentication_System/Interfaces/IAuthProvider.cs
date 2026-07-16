using Kaddumi.UnityTools.Auth.Core;
using System;

namespace Kaddumi.UnityTools.Auth.Interfaces
{
    /// <summary>
    /// Contract every authentication backend implements. A provider may support one or
    /// several <see cref="AuthMethod"/>s; the <see cref="Core.AuthService"/> routes each
    /// request to a provider that reports support for the requested method.
    /// </summary>
    public interface IAuthProvider
    {
        bool IsInitialized { get; }

        /// <summary>The currently signed-in user for this provider, or null.</summary>
        AuthUser CurrentUser { get; }

        bool IsSignedIn { get; }

        void Initialize(Action onComplete);

        /// <summary>True if this provider can handle the given sign-in method.</summary>
        bool SupportsMethod(AuthMethod method);

        void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete);

        /// <summary>Attach an additional sign-in method to the already signed-in account.</summary>
        void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete);

        void SignOut(Action onComplete);

        void DeleteAccount(Action<AuthResult> onComplete);

        event Action<AuthUser> OnSignedIn;
        event Action OnSignedOut;
    }
}
