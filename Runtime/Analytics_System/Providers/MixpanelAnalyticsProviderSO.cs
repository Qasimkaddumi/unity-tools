using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    [CreateAssetMenu(fileName = "MixpanelAnalyticsProvider", menuName = "Kaddumi/Analytics/Providers/Mixpanel")]
    public class MixpanelAnalyticsProviderSO : AnalyticsProviderSO
    {
        public override IAnalyticsProvider CreateProvider() => new MixpanelAnalyticsProvider();
    }
}
