using Kaddumi.UnityTools.Ads.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads.Providers
{
    [CreateAssetMenu(fileName = "UnityAdsProvider", menuName = "Kaddumi/Ads/Providers/Unity Ads")]
    public class UnityAdsProviderSO : AdProviderSO
    {
        public override IAdProvider CreateProvider() => new UnityAdsProvider();
    }
}
