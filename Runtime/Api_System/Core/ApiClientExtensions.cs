using System.Threading;
using System.Threading.Tasks;
using Kaddumi.UnityTools.Api.Interfaces;

namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// Verb-shaped convenience methods over <see cref="IApiClient"/> so everyday calls
    /// read as one-liners — <c>await client.GetAsync&lt;Profile&gt;("players/42")</c> —
    /// without every caller assembling an <see cref="ApiRequest"/>. Reach for
    /// <c>IApiClient.SendAsync</c> with a hand-built request only when you need headers,
    /// query params, a custom timeout, or anonymous access.
    /// </summary>
    public static class ApiClientExtensions
    {
        // --- GET ---
        public static Task<ApiResponse<T>> GetAsync<T>(this IApiClient client, string path, CancellationToken ct = default)
            => client.SendAsync<T>(ApiRequest.Get(path), ct);

        public static Task<ApiResponse> GetAsync(this IApiClient client, string path, CancellationToken ct = default)
            => client.SendAsync(ApiRequest.Get(path), ct);

        // --- POST ---
        public static Task<ApiResponse<T>> PostAsync<T>(this IApiClient client, string path, object body, CancellationToken ct = default)
            => client.SendAsync<T>(ApiRequest.Post(path).WithBody(body), ct);

        public static Task<ApiResponse> PostAsync(this IApiClient client, string path, object body, CancellationToken ct = default)
            => client.SendAsync(ApiRequest.Post(path).WithBody(body), ct);

        // --- PUT ---
        public static Task<ApiResponse<T>> PutAsync<T>(this IApiClient client, string path, object body, CancellationToken ct = default)
            => client.SendAsync<T>(ApiRequest.Put(path).WithBody(body), ct);

        public static Task<ApiResponse> PutAsync(this IApiClient client, string path, object body, CancellationToken ct = default)
            => client.SendAsync(ApiRequest.Put(path).WithBody(body), ct);

        // --- PATCH ---
        public static Task<ApiResponse<T>> PatchAsync<T>(this IApiClient client, string path, object body, CancellationToken ct = default)
            => client.SendAsync<T>(ApiRequest.Patch(path).WithBody(body), ct);

        public static Task<ApiResponse> PatchAsync(this IApiClient client, string path, object body, CancellationToken ct = default)
            => client.SendAsync(ApiRequest.Patch(path).WithBody(body), ct);

        // --- DELETE ---
        public static Task<ApiResponse> DeleteAsync(this IApiClient client, string path, CancellationToken ct = default)
            => client.SendAsync(ApiRequest.Delete(path), ct);

        public static Task<ApiResponse<T>> DeleteAsync<T>(this IApiClient client, string path, CancellationToken ct = default)
            => client.SendAsync<T>(ApiRequest.Delete(path), ct);
    }
}
