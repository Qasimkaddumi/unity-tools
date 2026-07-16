using Kaddumi.UnityTools.Ads.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads.Providers
{
    [CreateAssetMenu(fileName = "AdMobProvider", menuName = "Kaddumi/Ads/Providers/AdMob")]
    public class AdMobProviderSO : AdProviderSO
    {
        public override IAdProvider CreateProvider() => new AdMobProvider();
    }
}
