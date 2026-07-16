using System.Collections.Generic;

namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// A single HTTP request, built fluently and handed to <see cref="ApiClient"/>.
    /// It is transport-agnostic: it names a verb, a path (relative to the client's
    /// base URL, or an absolute URL), optional query parameters, headers, and a body.
    /// The client turns it into a <c>UnityWebRequest</c>, so systems never touch
    /// networking types directly.
    ///
    /// <para>The body can be a plain object (serialized by the client's
    /// <see cref="Interfaces.IApiSerializer"/>), a pre-serialized <see cref="string"/>,
    /// or raw <see cref="byte"/>s — see <see cref="Body"/>.</para>
    /// </summary>
    public sealed class ApiRequest
    {
        public HttpMethod Method { get; private set; }

        /// <summary>Relative path (combined with the client's base URL) or an absolute <c>http(s)://</c> URL.</summary>
        public string Path { get; private set; }

        /// <summary>
        /// Request body. If <c>null</c>, no body is sent. A <see cref="string"/> or
        /// <see cref="byte"/>[] is sent verbatim; anything else is JSON-serialized by
        /// the client.
        /// </summary>
        public object Body { get; private set; }

        /// <summary>Per-request headers (added on top of the client's default headers).</summary>
        public IReadOnlyDictionary<string, string> Headers => headers;

        /// <summary>Query-string parameters, appended and URL-escaped by the client.</summary>
        public IReadOnlyDictionary<string, string> Query => query;

        /// <summary>Per-request timeout override in seconds; <c>0</c> uses the client default.</summary>
        public int TimeoutSeconds { get; private set; }

        /// <summary>When <c>true</c>, the client attaches the auth token; defaults to <c>true</c>.</summary>
        public bool RequiresAuth { get; private set; } = true;

        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();
        private readonly Dictionary<string, string> query = new Dictionary<string, string>();

        private ApiRequest(HttpMethod method, string path)
        {
            Method = method;
            Path = path;
        }

        // --- Factory methods (read at call sites like Api.Get("...")) ---
        public static ApiRequest Get(string path) => new ApiRequest(HttpMethod.Get, path);
        public static ApiRequest Post(string path) => new ApiRequest(HttpMethod.Post, path);
        public static ApiRequest Put(string path) => new ApiRequest(HttpMethod.Put, path);
        public static ApiRequest Patch(string path) => new ApiRequest(HttpMethod.Patch, path);
        public static ApiRequest Delete(string path) => new ApiRequest(HttpMethod.Delete, path);

        /// <summary>Sets the request body (see <see cref="Body"/> for accepted types).</summary>
        public ApiRequest WithBody(object body)
        {
            Body = body;
            return this;
        }

        public ApiRequest WithHeader(string name, string value)
        {
            if (!string.IsNullOrEmpty(name)) headers[name] = value;
            return this;
        }

        public ApiRequest WithQuery(string name, string value)
        {
            if (!string.IsNullOrEmpty(name)) query[name] = value;
            return this;
        }

        public ApiRequest WithTimeout(int seconds)
        {
            TimeoutSeconds = seconds;
            return this;
        }

        /// <summary>Opts this request out of auth (no token header attached).</summary>
        public ApiRequest Anonymous()
        {
            RequiresAuth = false;
            return this;
        }
    }
}
