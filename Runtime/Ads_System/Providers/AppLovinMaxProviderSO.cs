using Kaddumi.UnityTools.Ads.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads.Providers
{
    [CreateAssetMenu(fileName = "AppLovinMaxProvider", menuName = "Kaddumi/Ads/Providers/AppLovin MAX")]
    public class AppLovinMaxProviderSO : AdProviderSO
    {
        public override IAdProvider CreateProvider() => new AppLovinMaxProvider();
    }
}
