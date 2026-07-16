using Kaddumi.UnityTools.Auth.Core;
using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Fully in-memory provider that fakes a successful sign-in for every method.
    /// Mirrors the analytics DebugLogger provider: it always compiles (no SDK required)
    /// and is ideal for editor iteration and testing UI flows before wiring a real SDK.
    /// </summary>
    public class DebugAuthProvider : IAuthProvider
    {
        public bool IsInitialized { get; private set; }
        public AuthUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;

        public event Action<AuthUser> OnSignedIn;
        public event Action OnSignedOut;

        public void Initialize(Action onComplete)
        {
            IsInitialized = true;
            Debug.Log("<color=lime>[Auth-Debug]</color> Debug Auth Provider Initialized");
            onComplete?.Invoke();
        }

        // The debug provider pretends to support everything.
        public bool SupportsMethod(AuthMethod method) => true;

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            var user = BuildFakeUser(credentials);
            CurrentUser = user;
            Debug.Log($"<color=lime>[Auth-Debug]</color> Signed in via {credentials.Method} as {user.UserId}");
            OnSignedIn?.Invoke(user);
            onComplete?.Invoke(AuthResult.Ok(user));
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            if (!IsSignedIn)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn, "Not signed in."));
                return;
            }

            var methods = new List<AuthMethod>(CurrentUser.Methods);
            if (!methods.Contains(credentials.Method)) methods.Add(credentials.Method);

            CurrentUser = new AuthUser(CurrentUser.UserId, methods, CurrentUser.DisplayName, CurrentUser.Email, false);
            Debug.Log($"<color=lime>[Auth-Debug]</color> Linked {credentials.Method} to {CurrentUser.UserId}");
            onComplete?.Invoke(AuthResult.Ok(CurrentUser));
        }

        public void SignOut(Action onComplete)
        {
            Debug.Log("<color=lime>[Auth-Debug]</color> Signed out");
            CurrentUser = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke();
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
            Debug.Log("<color=lime>[Auth-Debug]</color> Account deleted");
            CurrentUser = null;
            OnSignedOut?.Invoke();
            onComplete?.Invoke(AuthResult.Ok(null));
        }

        private static AuthUser BuildFakeUser(AuthCredentials credentials)
        {
            bool isGuest = credentials.Method == AuthMethod.Guest;
            string id = $"debug_{credentials.Method}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            string name = credentials.DisplayName ?? (isGuest ? "Guest" : credentials.Method.ToString() + " User");
            return new AuthUser(id, credentials.Method, name, credentials.Email, isGuest);
        }
    }
}
