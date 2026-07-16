using Kaddumi.UnityTools.Api.Core;
using Kaddumi.UnityTools.Api.Interfaces;
using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Interfaces;
using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    /// <summary>
    /// Stores each slot payload on a remote backend over HTTP, so saves survive a
    /// re-install and follow the player across devices. It does not talk to sockets
    /// itself: it rides the shared <see cref="IApiClient"/> from the Api system, which
    /// already handles the base URL, timeouts, JSON, and — crucially — the auth token,
    /// so cloud saves are scoped to whoever is signed in.
    ///
    /// <para>REST convention (all relative to the configured resource path, default
    /// <c>saves</c>): <c>PUT saves/{key}</c> writes, <c>GET saves/{key}</c> reads,
    /// <c>DELETE saves/{key}</c> deletes, and <c>GET saves</c> lists the current keys.
    /// The player is identified by the auth token, so the key is just the slot id.</para>
    ///
    /// <para>Bodies use a small JSON envelope (<see cref="Envelope"/>) so the opaque
    /// payload string round-trips cleanly whether or not it is itself JSON. Reads are
    /// tolerant: a backend that returns the raw payload instead of an envelope still
    /// works.</para>
    /// </summary>
    public class CloudSaveProvider : ISaveProvider
    {
        private readonly Func<IApiClient> clientResolver;
        private readonly string resourcePath;
        private readonly int timeoutSeconds;

        public bool IsInitialized { get; private set; }

        /// <param name="clientResolver">
        /// Resolves the API client at call time (not construction time), so it works even
        /// when the client is built by another manager that initializes after this one.
        /// </param>
        /// <param name="resourcePath">Collection path on the backend, e.g. <c>saves</c>.</param>
        /// <param name="timeoutSeconds">Per-request timeout; <c>0</c> uses the client default.</param>
        public CloudSaveProvider(Func<IApiClient> clientResolver, string resourcePath = "saves", int timeoutSeconds = 15)
        {
            this.clientResolver = clientResolver ?? throw new ArgumentNullException(nameof(clientResolver));
            this.resourcePath = string.IsNullOrEmpty(resourcePath) ? "saves" : resourcePath.Trim('/');
            this.timeoutSeconds = Mathf.Max(0, timeoutSeconds);
        }

        public void Initialize(Action onComplete)
        {
            if (clientResolver() == null)
            {
                Debug.LogWarning("<color=cyan>[Save-Cloud]</color> No API client available yet. " +
                                 "Ensure an ApiManager (or a base URL) is configured; requests will fail until one is.");
            }
            else
            {
                Debug.Log($"<color=cyan>[Save-Cloud]</color> Initialized against '{resourcePath}'");
            }

            // Ready to accept calls regardless — the client is resolved lazily per request,
            // so a client that comes online later is picked up automatically.
            IsInitialized = true;
            onComplete?.Invoke();
        }

        public async void Write(string key, string data, Action<SaveResult> onComplete)
        {
            if (!TryGetClient(out IApiClient client, out SaveResult error))
            {
                onComplete?.Invoke(error);
                return;
            }

            try
            {
                ApiRequest request = ApiRequest.Put(ResourceFor(key))
                    .WithBody(new Envelope { key = key, payload = data });
                if (timeoutSeconds > 0) request.WithTimeout(timeoutSeconds);

                ApiResponse response = await client.SendAsync(request);
                onComplete?.Invoke(response.IsSuccess
                    ? SaveResult.Ok()
                    : ToFailure(response, key));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Io, e.Message));
            }
        }

        public async void Read(string key, Action<SaveResult> onComplete)
        {
            if (!TryGetClient(out IApiClient client, out SaveResult error))
            {
                onComplete?.Invoke(error);
                return;
            }

            try
            {
                ApiRequest request = ApiRequest.Get(ResourceFor(key));
                if (timeoutSeconds > 0) request.WithTimeout(timeoutSeconds);

                ApiResponse response = await client.SendAsync(request);
                if (!response.IsSuccess)
                {
                    onComplete?.Invoke(ToFailure(response, key));
                    return;
                }

                onComplete?.Invoke(SaveResult.Ok(UnwrapPayload(response.RawBody)));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Corrupted, e.Message));
            }
        }

        public async void Delete(string key, Action<SaveResult> onComplete)
        {
            if (!TryGetClient(out IApiClient client, out SaveResult error))
            {
                onComplete?.Invoke(error);
                return;
            }

            try
            {
                ApiRequest request = ApiRequest.Delete(ResourceFor(key));
                if (timeoutSeconds > 0) request.WithTimeout(timeoutSeconds);

                ApiResponse response = await client.SendAsync(request);

                // Deleting something already gone is a success from the caller's point of view.
                onComplete?.Invoke(response.IsSuccess || response.IsNotFound
                    ? SaveResult.Ok()
                    : ToFailure(response, key));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Io, e.Message));
            }
        }

        public async void Exists(string key, Action<bool> onComplete)
        {
            IApiClient client = clientResolver();
            if (client == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            try
            {
                ApiRequest request = ApiRequest.Get(ResourceFor(key));
                if (timeoutSeconds > 0) request.WithTimeout(timeoutSeconds);

                ApiResponse response = await client.SendAsync(request);
                if (!response.IsSuccess && !response.IsNotFound)
                {
                    Debug.LogWarning($"[Save-Cloud] Exists('{key}') check failed: {Describe(response)}");
                }
                onComplete?.Invoke(response.IsSuccess);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save-Cloud] Exists('{key}') check errored: {e.Message}");
                onComplete?.Invoke(false);
            }
        }

        public async void List(Action<string[]> onComplete)
        {
            IApiClient client = clientResolver();
            if (client == null)
            {
                onComplete?.Invoke(Array.Empty<string>());
                return;
            }

            try
            {
                ApiRequest request = ApiRequest.Get(resourcePath);
                if (timeoutSeconds > 0) request.WithTimeout(timeoutSeconds);

                ApiResponse<KeyList> response = await client.SendAsync<KeyList>(request);
                onComplete?.Invoke(response.IsSuccess && response.Data?.keys != null
                    ? response.Data.keys
                    : Array.Empty<string>());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save-Cloud] List() failed: {e.Message}");
                onComplete?.Invoke(Array.Empty<string>());
            }
        }

        // --- Helpers ----------------------------------------------------------

        private string ResourceFor(string key) => $"{resourcePath}/{Uri.EscapeDataString(key)}";

        private bool TryGetClient(out IApiClient client, out SaveResult error)
        {
            client = clientResolver();
            if (client != null)
            {
                error = default;
                return true;
            }
            error = SaveResult.Fail(SaveErrorType.ProviderNotAvailable,
                "No API client is available for cloud saves.");
            return false;
        }

        /// <summary>
        /// Pulls the payload out of the JSON envelope, falling back to the raw body for
        /// backends that store/return the payload string directly.
        /// </summary>
        private static string UnwrapPayload(string rawBody)
        {
            if (string.IsNullOrEmpty(rawBody)) return rawBody;
            try
            {
                Envelope envelope = JsonUtility.FromJson<Envelope>(rawBody);
                if (envelope != null && envelope.payload != null) return envelope.payload;
            }
            catch
            {
                // Not an envelope — treat the body as the payload itself.
            }
            return rawBody;
        }

        private static SaveResult ToFailure(ApiResponse response, string key)
        {
            if (response.IsNotFound)
                return SaveResult.Fail(SaveErrorType.NotFound, $"No cloud save for key '{key}'.", 404);

            // SaveErrorType has no dedicated Network bucket; Io covers both a transport
            // failure (StatusCode 0 — timeout/offline) and a non-2xx server reply.
            return SaveResult.Fail(SaveErrorType.Io, Describe(response), (int)response.StatusCode);
        }

        private static string Describe(ApiResponse response) =>
            $"HTTP {response.StatusCode}: {response.Error ?? "request failed"}";

        // --- Wire DTOs (JsonUtility-friendly; top-level arrays must be wrapped) ------

        [Serializable]
        private class Envelope
        {
            public string key;
            public string payload;
        }

        [Serializable]
        private class KeyList
        {
            public string[] keys;
        }
    }
}
