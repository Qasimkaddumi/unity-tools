namespace Kaddumi.UnityTools.Api.Core
{
    /// <summary>
    /// The HTTP verbs the <see cref="ApiClient"/> understands. Kept as a small enum
    /// (rather than raw strings at call sites) so requests read clearly and typos are
    /// caught by the compiler; the client maps each to its wire verb.
    /// </summary>
    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Patch,
        Delete
    }
}
