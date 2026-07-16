using Kaddumi.UnityTools.Analytics.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Analytics.Providers
{
    [CreateAssetMenu(fileName = "DebugLoggerProvider", menuName = "Kaddumi/Analytics/Providers/Debug Logger")]
    public class DebugLoggerProviderSO : AnalyticsProviderSO
    {
        public override IAnalyticsProvider CreateProvider() => new DebugLoggerProvider();
    }
}
