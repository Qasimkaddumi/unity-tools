using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    [CreateAssetMenu(fileName = "PlayFabAuthProvider", menuName = "Kaddumi/Auth/Providers/PlayFab")]
    public class PlayFabAuthProviderSO : AuthProviderSO
    {
        public override IAuthProvider CreateProvider() => new PlayFabAuthProvider();
    }
}
