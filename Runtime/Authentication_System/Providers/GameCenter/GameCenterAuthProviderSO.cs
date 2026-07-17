using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    [CreateAssetMenu(fileName = "GameCenterAuthProvider", menuName = "Kaddumi/Auth/Providers/Game Center")]
    public class GameCenterAuthProviderSO : AuthProviderSO
    {
        public override IAuthProvider CreateProvider() => new GameCenterAuthProvider();
    }
}
