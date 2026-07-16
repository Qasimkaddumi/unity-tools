using Kaddumi.UnityTools.Ads.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads.Providers
{
    [CreateAssetMenu(fileName = "LevelPlayProvider", menuName = "Kaddumi/Ads/Providers/LevelPlay")]
    public class LevelPlayProviderSO : AdProviderSO
    {
        public override IAdProvider CreateProvider() => new LevelPlayProvider();
    }
}
