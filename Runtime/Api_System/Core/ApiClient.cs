using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kaddumi.UnityTools.Api.Interfaces;
using Kaddumi.UnityTools.Api.Internal;
using Kaddumi.UnityTools.Api.Serialization;
using UnityEngine.Networking;

namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// The reusable HTTP client every system talks to through <see cref="IApiClient"/>.
    /// A plain C# class (no MonoBehaviour): it drives <see cref="UnityWebRequest"/> via
    /// its async operation, so it needs no coroutine host and can be constructed and
    /// injected anywhere (including tests). <c>ApiManager</c> builds and shares one
    /// instance, but repositories may also be handed their own.
    ///
    /// <para>Responsibilities: build the URL (base + path + query), attach default and
    /// per-request headers, attach the auth token, (de)serialize bodies, and normalise
    /// the result into an <see cref="ApiResponse"/>. It never throws for HTTP error
    /// statuses — callers decide what a given status means.</para>
    /// </summary>
    public sealed class ApiClient : IApiClient
    {
        private readonly ApiClientOptions options;
        private readonly IApiSerializer serializer;
        private readonly IAuthTokenProvider authTokenProvider;

        /// <param name="options">Base URL, timeout, auth and default headers.</param>
        /// <param name="serializer">Body (de)serializer; defaults to <see cref="UnityJsonSerializer"/>.</param>
        /// <param name="authTokenProvider">Optional; queried per request for the auth token.</param>
        public ApiClient(ApiClientOptions options, IApiSerializer serializer = null, IAuthTokenProvider authTokenProvider = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.serializer = serializer ?? new UnityJsonSerializer();
            this.authTokenProvider = authTokenProvider;
        }

        public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
        {
            using (UnityWebRequest uwr = Build(request))
            {
                return await Execute(uwr, cancellationToken);
            }
        }

        public async Task<ApiResponse<TResponse>> SendAsync<TResponse>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            using (UnityWebRequest uwr = Build(request))
            {
                ApiResponse raw = await Execute(uwr, cancellationToken);

                TResponse data = default;
                if (raw.IsSuccess && !string.IsNullOrWhiteSpace(raw.RawBody))
                {
                    try
                    {
                        data = serializer.Deserialize<TResponse>(raw.RawBody);
                    }
                    catch (Exception e)
                    {
                        return new ApiResponse<TResponse>(
                            raw.StatusCode, false, raw.RawBody,
                            $"Failed to deserialize response into {typeof(TResponse).Name}: {e.Message}", default);
                    }
                }

                return new ApiResponse<TResponse>(raw.StatusCode, raw.IsSuccess, raw.RawBody, raw.Error, data);
            }
        }

        // --- Building -----------------------------------------------------------

        private UnityWebRequest Build(ApiRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var uwr = new UnityWebRequest(BuildUrl(request), Verb(request.Method))
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : Math.Max(1, options.DefaultTimeoutSeconds)
            };

            AttachBody(uwr, request);

            // Default headers first, then per-request headers so the request wins on clashes.
            foreach (KeyValuePair<string, string> header in options.DefaultHeaders)
                uwr.SetRequestHeader(header.Key, header.Value);
            foreach (KeyValuePair<string, string> header in request.Headers)
                uwr.SetRequestHeader(header.Key, header.Value);

            AttachAuth(uwr, request);
            return uwr;
        }

        private void AttachBody(UnityWebRequest uwr, ApiRequest request)
        {
            if (request.Body == null) return;

            byte[] bytes;
            string contentType = serializer.ContentType;

            switch (request.Body)
            {
                case byte[] raw:
                    bytes = raw;
                    break;
                case string str:
                    bytes = Encoding.UTF8.GetBytes(str);
                    break;
                default:
                    bytes = Encoding.UTF8.GetBytes(serializer.Serialize(request.Body));
                    break;
            }

            uwr.uploadHandler = new UploadHandlerRaw(bytes) { contentType = contentType };
        }

        private void AttachAuth(UnityWebRequest uwr, ApiRequest request)
        {
            if (!request.RequiresAuth || string.IsNullOrEmpty(options.AuthHeaderName)) return;

            string token = authTokenProvider?.GetToken();
            if (string.IsNullOrEmpty(token)) return;

            uwr.SetRequestHeader(options.AuthHeaderName, (options.AuthScheme ?? string.Empty) + token);
        }

        private string BuildUrl(ApiRequest request)
        {
            string path = request.Path ?? string.Empty;
            bool absolute = path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            string url = absolute
                ? path
                : $"{(options.BaseUrl ?? string.Empty).TrimEnd('/')}/{path.TrimStart('/')}";

            if (request.Query.Count == 0) return url;

            var sb = new StringBuilder(url);
            sb.Append(url.Contains("?") ? '&' : '?');
            bool first = true;
            foreach (KeyValuePair<string, string> pair in request.Query)
            {
                if (!first) sb.Append('&');
                sb.Append(UnityWebRequest.EscapeURL(pair.Key))
                  .Append('=')
                  .Append(UnityWebRequest.EscapeURL(pair.Value ?? string.Empty));
                first = false;
            }
            return sb.ToString();
        }

        // --- Executing ----------------------------------------------------------

        private static async Task<ApiResponse> Execute(UnityWebRequest uwr, CancellationToken cancellationToken)
        {
            try
            {
                await uwr.SendAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new ApiResponse(0, false, null, "Request was cancelled.");
            }

            string body = uwr.downloadHandler != null ? uwr.downloadHandler.text : null;
            bool success = uwr.result == UnityWebRequest.Result.Success;

            if (success) return new ApiResponse(uwr.responseCode, true, body, null);

            string error = string.IsNullOrEmpty(uwr.error) ? uwr.result.ToString() : uwr.error;
            return new ApiResponse(uwr.responseCode, false, body, error);
        }

        private static string Verb(HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.Get: return UnityWebRequest.kHttpVerbGET;
                case HttpMethod.Post: return UnityWebRequest.kHttpVerbPOST;
                case HttpMethod.Put: return UnityWebRequest.kHttpVerbPUT;
                case HttpMethod.Delete: return UnityWebRequest.kHttpVerbDELETE;
                case HttpMethod.Patch: return "PATCH";
                default: return UnityWebRequest.kHttpVerbGET;
            }
        }
    }
}
