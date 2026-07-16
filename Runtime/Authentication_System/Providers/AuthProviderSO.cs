using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    /// <summary>
    /// Base ScriptableObject that acts as a factory for an <see cref="IAuthProvider"/>.
    /// Create a concrete asset per provider and assign it in the AuthManager inspector
    /// to switch backends without touching code.
    /// </summary>
    public abstract class AuthProviderSO : ScriptableObject
    {
        /// <summary>Creates a fresh runtime provider instance.</summary>
        public abstract IAuthProvider CreateProvider();
    }
}
