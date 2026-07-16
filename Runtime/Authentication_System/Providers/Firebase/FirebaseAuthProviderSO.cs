using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    [CreateAssetMenu(fileName = "FirebaseAuthProvider", menuName = "Kaddumi/Auth/Providers/Firebase")]
    public class FirebaseAuthProviderSO : AuthProviderSO
    {
        public override IAuthProvider CreateProvider() => new FirebaseAuthProvider();
    }
}
