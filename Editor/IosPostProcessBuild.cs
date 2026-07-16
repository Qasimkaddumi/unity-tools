#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Kaddumi.UnityTools.IosBuild.Editor
{
    public static class IosPostProcessBuild
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            ProcessInfoPlist(pathToBuiltProject);
        }

        private static void ProcessInfoPlist(string pathToBuiltProject)
        {
            string infoPlistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            PlistDocument plistDocument = new PlistDocument();

            plistDocument.ReadFromString(File.ReadAllText(infoPlistPath));
            PlistElementDict rootDictionary = plistDocument.root;


#if AdMob_SDK_INSTALLED
            AddUserTrackingDescription(rootDictionary);
#endif

#if MetaAds_SDK_INSTALLED
            AddSkAdNetworkIdentifiers(rootDictionary);
#endif
            File.WriteAllText(infoPlistPath, plistDocument.WriteToString());
        }

        private static void AddUserTrackingDescription(PlistElementDict rootDictionary)
        {
            string trackingDescriptionKey = "NSUserTrackingUsageDescription";
            string trackingDescription = "We use tracking data to provide personalized ads and improve game performance.";

            rootDictionary.SetString(trackingDescriptionKey, trackingDescription);
        }

        private static void AddSkAdNetworkIdentifiers(PlistElementDict rootDictionary)
        {
            string skAdNetworkItemsKey = "SKAdNetworkItems";
            PlistElementArray skAdNetworkItemsArray;

            // Check if the array already exists to prevent overwriting other SDKs
            if (rootDictionary.values.TryGetValue(skAdNetworkItemsKey, out PlistElement existingElement))
            {
                skAdNetworkItemsArray = existingElement as PlistElementArray;
            }
            else
            {
                skAdNetworkItemsArray = rootDictionary.CreateArray(skAdNetworkItemsKey);
            }

            string[] skAdNetworkIds = new string[]
            {
                "v9wttpbfk9.skadnetwork",
                "n38lu8286q.skadnetwork"
            };

            foreach (string skAdId in skAdNetworkIds)
            {
                PlistElementDict skAdDict = skAdNetworkItemsArray.AddDict();
                skAdDict.SetString("SKAdNetworkIdentifier", skAdId);
            }
        }
    }
}
#endif