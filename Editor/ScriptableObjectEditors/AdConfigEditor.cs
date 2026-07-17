using Kaddumi.UnityTools.Ads.Data;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(AdConfig))]
    public class AdConfigEditor : Editor
    {
        private SerializedProperty _androidAppId, _iosAppId;
        private SerializedProperty _androidBanner, _iosBanner;
        private SerializedProperty _androidInterstitial, _iosInterstitial;
        private SerializedProperty _androidRewarded, _iosRewarded;

        private void OnEnable()
        {
            _androidAppId = serializedObject.FindProperty(nameof(AdConfig.AndroidAppId));
            _iosAppId = serializedObject.FindProperty(nameof(AdConfig.IOSAppId));
            _androidBanner = serializedObject.FindProperty(nameof(AdConfig.AndroidBannerId));
            _iosBanner = serializedObject.FindProperty(nameof(AdConfig.IOSBannerId));
            _androidInterstitial = serializedObject.FindProperty(nameof(AdConfig.AndroidInterstitialId));
            _iosInterstitial = serializedObject.FindProperty(nameof(AdConfig.IOSInterstitialId));
            _androidRewarded = serializedObject.FindProperty(nameof(AdConfig.AndroidRewardedId));
            _iosRewarded = serializedObject.FindProperty(nameof(AdConfig.IOSRewardedId));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Ad Config", "d_UnityEditor.GameView");

            SOEditorKit.PlatformField(_androidAppId, _iosAppId, "App ID");
            EditorGUILayout.Space(6);

            SOEditorKit.SectionHeader("Banner", "d_align_horizontally");
            SOEditorKit.PlatformField(_androidBanner, _iosBanner, string.Empty);
            EditorGUILayout.Space(6);

            SOEditorKit.SectionHeader("Interstitial", "d_FullscreenView");
            SOEditorKit.PlatformField(_androidInterstitial, _iosInterstitial, string.Empty);
            EditorGUILayout.Space(6);

            SOEditorKit.SectionHeader("Rewarded", "d_Favorite");
            SOEditorKit.PlatformField(_androidRewarded, _iosRewarded, string.Empty);

            bool anyMissing = string.IsNullOrEmpty(_androidBanner.stringValue) && string.IsNullOrEmpty(_iosBanner.stringValue)
                && string.IsNullOrEmpty(_androidInterstitial.stringValue) && string.IsNullOrEmpty(_iosInterstitial.stringValue)
                && string.IsNullOrEmpty(_androidRewarded.stringValue) && string.IsNullOrEmpty(_iosRewarded.stringValue);
            SOEditorKit.MissingWarning(anyMissing, "No ad unit IDs are set for either platform yet — GetAdUnitId will return empty strings.");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
