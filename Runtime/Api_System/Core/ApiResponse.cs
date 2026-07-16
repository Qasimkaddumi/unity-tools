namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// The outcome of an API call, without a deserialized body. The client never
    /// throws for a non-2xx status — it returns this so callers can branch on
    /// <see cref="StatusCode"/> (e.g. treat 404 as "not found" rather than an error).
    /// Callers who prefer exceptions can call <see cref="EnsureSuccess"/>.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>HTTP status code, or <c>0</c> when no reply was received (transport/timeout/cancel).</summary>
        public long StatusCode { get; }

        /// <summary><c>true</c> for a 2xx status.</summary>
        public bool IsSuccess { get; }

        /// <summary>Raw response body text, if any.</summary>
        public string RawBody { get; }

        /// <summary>Error detail for a failed request; <c>null</c> on success.</summary>
        public string Error { get; }

        public ApiResponse(long statusCode, bool isSuccess, string rawBody, string error)
        {
            StatusCode = statusCode;
            IsSuccess = isSuccess;
            RawBody = rawBody;
            Error = error;
        }

        /// <summary><c>true</c> when the server replied 404 Not Found.</summary>
        public bool IsNotFound => StatusCode == 404;

        /// <summary>Throws <see cref="ApiException"/> unless the request succeeded.</summary>
        public void EnsureSuccess()
        {
            if (IsSuccess) return;
            throw new ApiException(
                $"API request failed ({StatusCode}): {Error}", StatusCode, RawBody);
        }
    }

    /// <summary>
    /// An <see cref="ApiResponse"/> whose body has been deserialized into
    /// <typeparamref name="T"/>. <see cref="Data"/> is only populated on success (and
    /// when the body was non-empty).
    /// </summary>
    public sealed class ApiResponse<T> : ApiResponse
    {
        /// <summary>The deserialized body, or <c>default</c> when absent or the request failed.</summary>
        public T Data { get; }

        public ApiResponse(long statusCode, bool isSuccess, string rawBody, string error, T data)
            : base(statusCode, isSuccess, rawBody, error)
        {
            Data = data;
        }

        /// <summary>Throws unless the request succeeded, otherwise returns <see cref="Data"/>.</summary>
        public T EnsureSuccessData()
        {
            EnsureSuccess();
            return Data;
        }
    }
}
