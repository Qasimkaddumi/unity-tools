using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    [CreateAssetMenu(fileName = "FirebaseAnalyticsProvider", menuName = "Kaddumi/Analytics/Providers/Firebase")]
    public class FirebaseAnalyticsProviderSO : AnalyticsProviderSO
    {
        public override IAnalyticsProvider CreateProvider() => new FirebaseAnalyticsProvider();
    }
}
