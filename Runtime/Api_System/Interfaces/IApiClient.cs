using System.Threading;
using System.Threading.Tasks;
using Kaddumi.UnityTools.Api.Core;

namespace Kaddumi.UnityTools.Api.Interfaces
{
    /// <summary>
    /// The one contract every system uses to talk to a backend. It is deliberately
    /// tiny — two <c>SendAsync</c> overloads — with the ergonomic <c>GetAsync</c>/
    /// <c>PostAsync</c>/… helpers layered on top as extension methods
    /// (<see cref="ApiClientExtensions"/>). Depending on this interface (not the
    /// concrete <see cref="ApiClient"/> or the <c>ApiManager</c> singleton) keeps
    /// repositories testable: a fake <c>IApiClient</c> stands in for the network.
    ///
    /// <para>Neither overload throws for HTTP error statuses; inspect the returned
    /// <see cref="ApiResponse"/> (or call <c>EnsureSuccess</c>). Awaiting resumes on
    /// Unity's main thread, so it is safe to touch Unity objects afterwards.</para>
    /// </summary>
    public interface IApiClient
    {
        /// <summary>Sends <paramref name="request"/> and returns the raw outcome (no body deserialization).</summary>
        Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends <paramref name="request"/> and deserializes a successful, non-empty
        /// response body into <typeparamref name="TResponse"/>.
        /// </summary>
        Task<ApiResponse<TResponse>> SendAsync<TResponse>(ApiRequest request, CancellationToken cancellationToken = default);
    }
}
