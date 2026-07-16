using System;
using Kaddumi.UnityTools.Api.Interfaces;

namespace Kaddumi.UnityTools.Api.Auth
{
    /// <summary>
    /// The simplest <see cref="IAuthTokenProvider"/>: holds a token that a sign-in flow
    /// updates via <see cref="SetToken"/> (or hand it a <see cref="Func{String}"/> to
    /// read the token from wherever it already lives). Thread-agnostic — expected to be
    /// used from Unity's main thread. This is what <c>ApiManager</c> uses by default, so
    /// after login you simply call <c>ApiManager.Instance.SetAuthToken(token)</c>.
    /// </summary>
    public sealed class MutableAuthTokenProvider : IAuthTokenProvider
    {
        private string token;
        private readonly Func<string> source;

        public MutableAuthTokenProvider(string initialToken = null)
        {
            token = initialToken;
        }

        /// <param name="source">Read the token lazily from an external store on each request.</param>
        public MutableAuthTokenProvider(Func<string> source)
        {
            this.source = source;
        }

        public string GetToken() => source != null ? source() : token;

        /// <summary>Sets (or clears, with <c>null</c>) the token used for subsequent requests.</summary>
        public void SetToken(string value) => token = value;
    }
}
