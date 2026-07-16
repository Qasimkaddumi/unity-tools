using Kaddumi.UnityTools.Api.Auth;
using Kaddumi.UnityTools.Api.Config;
using Kaddumi.UnityTools.Api.Core;
using Kaddumi.UnityTools.Api.Interfaces;
using Kaddumi.UnityTools.Api.Serialization;
using Kaddumi.UnityTools.Services;
using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Api
{
    /// <summary>
    /// The API system's composition root and global access point — the networking
    /// counterpart to <c>AudioManager</c>. As the single persistent <see cref="Instance"/>
    /// it builds one <see cref="ApiClient"/> from an <see cref="ApiConfigSO"/> and exposes
    /// it as <see cref="Client"/>, so any system — present or future — can call
    /// <c>ApiManager.Instance.Client.GetAsync&lt;T&gt;(...)</c>.
    ///
    /// <para>It owns a <see cref="MutableAuthTokenProvider"/>, so a sign-in flow just calls
    /// <see cref="SetAuthToken"/> once and every subsequent request carries the token.
    /// Systems that prefer dependency injection can ignore the singleton and construct
    /// their own <see cref="ApiClient"/> — <see cref="Client"/> is only the shared default.</para>
    ///
    /// <para>Like the other managers it implements <see cref="IService"/>, so the shared
    /// <c>ServiceLocator</c> builds the client during its initialization pass. The client
    /// is ready synchronously, so <see cref="Initialize"/> completes immediately.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApiManager : MonoBehaviour, IService
    {
        public static ApiManager Instance { get; private set; }

        [Tooltip("Endpoint, auth and default-header configuration for the shared client.")]
        [SerializeField] private ApiConfigSO config;

        private MutableAuthTokenProvider tokenProvider;

        /// <summary>The shared, configured API client. Null only if no config was assigned.</summary>
        public IApiClient Client { get; private set; }

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
            if (config == null)
            {
                Debug.LogError($"[ApiManager] No {nameof(ApiConfigSO)} assigned; Client will be null.", this);
                onComplete?.Invoke();
                return;
            }

            tokenProvider = new MutableAuthTokenProvider();
            Client = new ApiClient(config.BuildOptions(), new UnityJsonSerializer(), tokenProvider);
            onComplete?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Sets (or clears, with <c>null</c>) the auth token attached to every subsequent request.</summary>
        public void SetAuthToken(string token) => tokenProvider?.SetToken(token);
    }
}
