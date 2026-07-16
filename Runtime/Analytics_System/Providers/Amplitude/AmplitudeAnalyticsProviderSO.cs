using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    [CreateAssetMenu(fileName = "AmplitudeAnalyticsProvider", menuName = "Kaddumi/Analytics/Providers/Amplitude")]
    public class AmplitudeAnalyticsProviderSO : AnalyticsProviderSO
    {
        public override IAnalyticsProvider CreateProvider() => new AmplitudeAnalyticsProvider();
    }
}
