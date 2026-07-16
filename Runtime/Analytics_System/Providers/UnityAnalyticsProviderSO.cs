using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    [CreateAssetMenu(fileName = "UnityAnalyticsProvider", menuName = "Kaddumi/Analytics/Providers/Unity Analytics")]
    public class UnityAnalyticsProviderSO : AnalyticsProviderSO
    {
        public override IAnalyticsProvider CreateProvider() => new UnityAnalyticsProvider();
    }
}
