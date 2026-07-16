using Kaddumi.UnityTools.Api;
using Kaddumi.UnityTools.Api.Core;
using Kaddumi.UnityTools.Api.Interfaces;
using Kaddumi.UnityTools.Api.Serialization;
using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    /// <summary>
    /// Inspector factory for a <see cref="CloudSaveProvider"/>. Assign it to the
    /// SaveManager to persist saves to an online database over HTTP.
    ///
    /// <para>By default it reuses the shared <c>ApiManager</c> client, which already
    /// carries the signed-in player's auth token — so cloud saves are per-user with no
    /// extra wiring (just call <c>ApiManager.Instance.SetAuthToken(...)</c> after login).
    /// Untick "Use Shared Api Client" to point at a standalone base URL instead, for a
    /// simple keyed backend that doesn't need the rest of the Api system.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CloudSaveProvider", menuName = "Kaddumi/Save/Providers/Cloud")]
    public class CloudSaveProviderSO : SaveProviderSO
    {
        [Header("Endpoint")]
        [Tooltip("Collection path saves live under on the backend (e.g. 'saves'). Combined with " +
                 "the slot key: PUT/GET/DELETE <resource>/<key>, and GET <resource> to list.")]
        [SerializeField] private string resourcePath = "saves";

        [Tooltip("Per-request timeout in seconds. 0 uses the API client's default.")]
        [SerializeField] private int timeoutSeconds = 15;

        [Header("Transport")]
        [Tooltip("Reuse the shared ApiManager client (recommended): it supplies the base URL and " +
                 "the signed-in player's auth token, so saves are scoped per-user automatically.")]
        [SerializeField] private bool useSharedApiClient = true;

        [Header("Standalone (used only when 'Use Shared Api Client' is off)")]
        [Tooltip("Root URL of the backend, e.g. https://api.example.com. No trailing slash needed.")]
        [SerializeField] private string baseUrl = "https://api.example.com";

        [Tooltip("Optional API key header sent on every request (name), e.g. 'X-Api-Key'. Leave empty for none.")]
        [SerializeField] private string apiKeyHeaderName = string.Empty;

        [Tooltip("Value for the API key header above.")]
        [SerializeField] private string apiKeyValue = string.Empty;

        public override ISaveProvider CreateProvider()
        {
            if (useSharedApiClient)
            {
                // Resolve lazily: ApiManager may initialize after the SaveManager, and its
                // token can change at sign-in — reading Client per request keeps up with both.
                return new CloudSaveProvider(() => ApiManager.Instance != null ? ApiManager.Instance.Client : null,
                    resourcePath, timeoutSeconds);
            }

            IApiClient standalone = BuildStandaloneClient();
            return new CloudSaveProvider(() => standalone, resourcePath, timeoutSeconds);
        }

        private IApiClient BuildStandaloneClient()
        {
            var options = new ApiClientOptions
            {
                BaseUrl = baseUrl,
                DefaultTimeoutSeconds = Mathf.Max(1, timeoutSeconds)
            };

            if (!string.IsNullOrEmpty(apiKeyHeaderName))
                options.DefaultHeaders[apiKeyHeaderName] = apiKeyValue;

            return new ApiClient(options, new UnityJsonSerializer());
        }
    }
}
