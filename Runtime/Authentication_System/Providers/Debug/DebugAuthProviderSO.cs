using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    [CreateAssetMenu(fileName = "DebugAuthProvider", menuName = "Kaddumi/Auth/Providers/Debug")]
    public class DebugAuthProviderSO : AuthProviderSO
    {
        public override IAuthProvider CreateProvider() => new DebugAuthProvider();
    }
}
