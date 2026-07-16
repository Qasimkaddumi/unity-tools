using Kaddumi.UnityTools.Ads.Core;
using UnityEngine;

namespace Kaddumi.UnityTools.Ads.Data
{
   
    [CreateAssetMenu(fileName = "AdConfig", menuName = "Ads/AdConfig")]
    public class AdConfig : ScriptableObject
    {
        [Header("App ID (Optional for some SDKs)")]
        public string AndroidAppId;
        public string IOSAppId;

        [Header("Banner IDs")]
        public string AndroidBannerId;
        public string IOSBannerId;

        [Header("Interstitial IDs")]
        public string AndroidInterstitialId;
        public string IOSInterstitialId;

        [Header("Rewarded IDs")]
        public string AndroidRewardedId;
        public string IOSRewardedId;

        public string GetAdUnitId(AdType type)
        {
            bool isAndroid = Application.platform == RuntimePlatform.Android;

            switch (type)
            {
                case AdType.Banner:
                    return isAndroid ? AndroidBannerId : IOSBannerId;
                case AdType.Interstitial:
                    return isAndroid ? AndroidInterstitialId : IOSInterstitialId;
                case AdType.Rewarded:
                    return isAndroid ? AndroidRewardedId : IOSRewardedId;
                default:
                    return string.Empty;
            }
        }
    }
}