using System;

namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// Thrown by <see cref="ApiResponse.EnsureSuccess"/> (and the typed overload) when a
    /// request did not succeed. Callers that prefer branch-on-status can inspect the
    /// <see cref="ApiResponse"/> directly instead of catching this. A
    /// <see cref="ResponseCode"/> of <c>0</c> means the request never got an HTTP reply
    /// (transport failure, timeout, or cancellation).
    /// </summary>
    public class ApiException : Exception
    {
        /// <summary>HTTP status code, or <c>0</c> for a transport/timeout/cancel failure.</summary>
        public long ResponseCode { get; }

        /// <summary>Raw response body, when the server sent one.</summary>
        public string Body { get; }

        public ApiException(string message, long responseCode, string body = null) : base(message)
        {
            ResponseCode = responseCode;
            Body = body;
        }
    }
}
