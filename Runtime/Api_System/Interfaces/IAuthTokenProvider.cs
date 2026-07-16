namespace Kaddumi.UnityTools.Api.Interfaces
{
    /// <summary>
    /// Supplies the current auth token to attach to outgoing requests. Queried on
    /// <em>every</em> request (not cached) so a token refreshed mid-session is picked
    /// up automatically. Return <c>null</c>/empty to send no auth header — e.g. before
    /// the player has signed in.
    /// </summary>
    public interface IAuthTokenProvider
    {
        /// <summary>The current token, or <c>null</c>/empty when there is none.</summary>
        string GetToken();
    }
}
