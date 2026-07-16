using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Api.Core;
using UnityEngine;

namespace Kaddumi.UnityTools.Api.Config
{
    /// <summary>
    /// Inspector-editable configuration for the shared <see cref="ApiClient"/> — one
    /// asset per environment (dev / staging / prod), swapped on the <c>ApiManager</c>
    /// without touching code. Mirrors how <c>AudioSettingsSO</c> drives the audio system.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ApiConfig", menuName = "Kaleem/Api/Api Config")]
    public class ApiConfigSO : ScriptableObject
    {
        [Serializable]
        public struct Header
        {
            public string name;
            public string value;
        }

        [Header("Endpoint")]
        [Tooltip("Root of the API, e.g. https://api.kaleem.example. No trailing slash needed.")]
        [SerializeField] private string baseUrl = "https://api.example.com";

        [Tooltip("Per-request timeout in seconds (a request can override this).")]
        [SerializeField] private int defaultTimeoutSeconds = 10;

        [Header("Auth")]
        [Tooltip("Header used to send the auth token (e.g. Authorization). Leave empty for no auth header.")]
        [SerializeField] private string authHeaderName = "Authorization";

        [Tooltip("Prefix prepended to the token, e.g. \"Bearer \" (include the trailing space).")]
        [SerializeField] private string authScheme = "Bearer ";

        [Header("Default headers")]
        [Tooltip("Headers sent on every request (e.g. an API key, Accept, a client version).")]
        [SerializeField] private List<Header> defaultHeaders = new List<Header>();

        public string BaseUrl => baseUrl;

        /// <summary>Builds the Unity-free options object the <see cref="ApiClient"/> consumes.</summary>
        public ApiClientOptions BuildOptions()
        {
            var options = new ApiClientOptions
            {
                BaseUrl = baseUrl,
                DefaultTimeoutSeconds = Mathf.Max(1, defaultTimeoutSeconds),
                AuthHeaderName = authHeaderName,
                AuthScheme = authScheme
            };

            if (defaultHeaders != null)
            {
                foreach (Header header in defaultHeaders)
                {
                    if (!string.IsNullOrEmpty(header.name))
                        options.DefaultHeaders[header.name] = header.value;
                }
            }
            return options;
        }
    }
}
