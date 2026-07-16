using System.Collections.Generic;

namespace Kaddumi.UnityTools.Auth.Core
{
    /// <summary>
    /// SDK-agnostic snapshot of the currently signed-in user. Providers translate their
    /// native user object into this so game code never depends on a specific SDK type.
    /// </summary>
    public class AuthUser
    {
        public string UserId { get; }
        public string DisplayName { get; }
        public string Email { get; }
        public bool IsGuest { get; }

        /// <summary>The method that produced this session (and any additionally linked methods).</summary>
        public IReadOnlyList<AuthMethod> Methods { get; }

        public AuthUser(string userId, AuthMethod method, string displayName = null, string email = null, bool isGuest = false)
            : this(userId, new List<AuthMethod> { method }, displayName, email, isGuest)
        {
        }

        public AuthUser(string userId, IReadOnlyList<AuthMethod> methods, string displayName = null, string email = null, bool isGuest = false)
        {
            UserId = userId;
            Methods = methods ?? new List<AuthMethod>();
            DisplayName = displayName;
            Email = email;
            IsGuest = isGuest;
        }

        public override string ToString() => $"AuthUser(Id={UserId}, Name={DisplayName}, Guest={IsGuest})";
    }
}
