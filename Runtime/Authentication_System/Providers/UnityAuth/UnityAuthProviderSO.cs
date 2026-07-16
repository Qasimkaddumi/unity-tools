using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    [CreateAssetMenu(fileName = "UnityAuthProvider", menuName = "Kaddumi/Auth/Providers/Unity Authentication")]
    public class UnityAuthProviderSO : AuthProviderSO
    {
        public override IAuthProvider CreateProvider() => new UnityAuthProvider();
    }
}
