using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    [CreateAssetMenu(fileName = "GameAnalyticsProvider", menuName = "Kaddumi/Analytics/Providers/GameAnalytics")]
    public class GameAnalyticsProviderSO : AnalyticsProviderSO
    {
        public override IAnalyticsProvider CreateProvider() => new GameAnalyticsProvider();
    }
}
