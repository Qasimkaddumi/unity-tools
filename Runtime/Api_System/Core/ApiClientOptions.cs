using System.Collections.Generic;

namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// Plain, Unity-free configuration for an <see cref="ApiClient"/>: base URL, default
    /// timeout, how the auth token is attached, and any headers sent on every request.
    /// Kept separate from <c>ApiConfigSO</c> so the client can be constructed and unit
    /// tested without a ScriptableObject (the SO simply builds one of these).
    /// </summary>
    public sealed class ApiClientOptions
    {
        /// <summary>Root URL prepended to relative request paths, e.g. <c>https://api.kaleem.example</c>.</summary>
        public string BaseUrl { get; set; }

        /// <summary>Fallback per-request timeout in seconds when a request doesn't override it.</summary>
        public int DefaultTimeoutSeconds { get; set; } = 10;

        /// <summary>Header used to carry the auth token, e.g. <c>Authorization</c>. Empty disables auth headers.</summary>
        public string AuthHeaderName { get; set; } = "Authorization";

        /// <summary>Prefix prepended to the token, e.g. <c>"Bearer "</c> (include the trailing space).</summary>
        public string AuthScheme { get; set; } = "Bearer ";

        /// <summary>Headers attached to every request (a request's own headers win on a key clash).</summary>
        public IDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>();
    }
}
