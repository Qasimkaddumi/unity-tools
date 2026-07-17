using Kaddumi.UnityTools.Auth.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Auth.Providers
{
    [CreateAssetMenu(fileName = "GooglePlayGamesAuthProvider", menuName = "Kaddumi/Auth/Providers/Google Play Games")]
    public class GooglePlayGamesAuthProviderSO : AuthProviderSO
    {
        [Tooltip("Also request a one-time server-side auth code on sign-in. Enable this only if you " +
                 "federate the Play Games identity into a backend (Firebase/PlayFab/Unity) via " +
                 "GooglePlayGamesAuthProvider.ServerAuthCode.")]
        [SerializeField] private bool requestServerAuthCode = false;

        public override IAuthProvider CreateProvider() => new GooglePlayGamesAuthProvider(requestServerAuthCode);
    }
}
