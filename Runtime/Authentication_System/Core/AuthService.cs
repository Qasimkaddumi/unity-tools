using Kaddumi.UnityTools.Auth.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Core
{
    /// <summary>
    /// Plain-C# domain service that owns the registered providers and routes each
    /// authentication request to a provider that supports the requested method.
    /// This is the SDK-agnostic heart of the system; <c>AuthManager</c> is only the
    /// MonoBehaviour host that feeds it providers from the inspector.
    /// </summary>
    public class AuthService
    {
        private readonly List<IAuthProvider> providers;

        /// <summary>The user from the most recent successful sign-in, or null.</summary>
        public AuthUser CurrentUser { get; private set; }

        public bool IsSignedIn => CurrentUser != null;

        public event Action<AuthUser> OnSignedIn;
        public event Action OnSignedOut;

        public AuthService()
        {
            providers = new List<IAuthProvider>();
        }

        public void RegisterProvider(IAuthProvider provider, Action onComplete)
        {
            if (provider == null)
            {
                onComplete?.Invoke();
                return;
            }

            provider.OnSignedIn += HandleProviderSignedIn;
            provider.OnSignedOut += HandleProviderSignedOut;

            providers.Add(provider);
            provider.Initialize(onComplete);
        }

        /// <summary>All sign-in methods supported by at least one registered provider.</summary>
        public IEnumerable<AuthMethod> AvailableMethods()
        {
            var seen = new HashSet<AuthMethod>();
            foreach (var provider in providers)
            {
                foreach (AuthMethod method in Enum.GetValues(typeof(AuthMethod)))
                {
                    if (provider.SupportsMethod(method)) seen.Add(method);
                }
            }
            return seen;
        }

        public bool IsMethodAvailable(AuthMethod method) => FindProvider(method) != null;

        public void SignIn(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            var provider = FindProvider(credentials.Method);
            if (provider == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                    $"No registered provider supports '{credentials.Method}'."));
                return;
            }

            provider.SignIn(credentials, result =>
            {
                if (result.Success) CurrentUser = result.User;
                onComplete?.Invoke(result);
            });
        }

        public void LinkMethod(AuthCredentials credentials, Action<AuthResult> onComplete)
        {
            if (!IsSignedIn)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn,
                    "Cannot link a method before signing in."));
                return;
            }

            var provider = FindProvider(credentials.Method);
            if (provider == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.ProviderNotAvailable,
                    $"No registered provider supports '{credentials.Method}'."));
                return;
            }

            provider.LinkMethod(credentials, result =>
            {
                if (result.Success) CurrentUser = result.User;
                onComplete?.Invoke(result);
            });
        }

        /// <summary>Signs out of every provider that currently holds a session.</summary>
        public void SignOut(Action onComplete)
        {
            int remaining = 0;
            foreach (var provider in providers)
            {
                if (provider.IsSignedIn) remaining++;
            }

            if (remaining == 0)
            {
                CurrentUser = null;
                onComplete?.Invoke();
                return;
            }

            foreach (var provider in providers)
            {
                if (!provider.IsSignedIn) continue;

                provider.SignOut(() =>
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        CurrentUser = null;
                        onComplete?.Invoke();
                    }
                });
            }
        }

        public void DeleteAccount(Action<AuthResult> onComplete)
        {
            if (!IsSignedIn)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn,
                    "Cannot delete an account before signing in."));
                return;
            }

            var provider = ProviderHoldingSession();
            if (provider == null)
            {
                onComplete?.Invoke(AuthResult.Fail(AuthErrorType.NotSignedIn,
                    "No provider holds an active session."));
                return;
            }

            provider.DeleteAccount(result =>
            {
                if (result.Success) CurrentUser = null;
                onComplete?.Invoke(result);
            });
        }

        private IAuthProvider FindProvider(AuthMethod method)
        {
            foreach (var provider in providers)
            {
                if (provider.SupportsMethod(method)) return provider;
            }
            return null;
        }

        private IAuthProvider ProviderHoldingSession()
        {
            foreach (var provider in providers)
            {
                if (provider.IsSignedIn) return provider;
            }
            return null;
        }

        private void HandleProviderSignedIn(AuthUser user)
        {
            CurrentUser = user;
            OnSignedIn?.Invoke(user);
        }

        private void HandleProviderSignedOut()
        {
            // Only clear the shared session once no provider holds one anymore.
            if (ProviderHoldingSession() == null)
            {
                CurrentUser = null;
                OnSignedOut?.Invoke();
            }
        }
    }
}
