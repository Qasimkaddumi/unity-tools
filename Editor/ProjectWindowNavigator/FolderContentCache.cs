using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public static class FolderContentCache
    {
        // Dictionary of folder GUID -> List of Asset Types found in it
        private static Dictionary<string, HashSet<System.Type>> folderContents = new Dictionary<string, HashSet<System.Type>>();

        public static HashSet<System.Type> GetFolderContents(string guid)
        {
            if (folderContents.TryGetValue(guid, out var types))
            {
                return types;
            }
            return null;
        }

        public static void MarkDirty(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            
            string parent = Path.GetDirectoryName(path);
            // Convert to unity path format
            parent = parent.Replace("\\", "/");
            
            if (!string.IsNullOrEmpty(parent))
            {
                string guid = AssetDatabase.AssetPathToGUID(parent);
                if (folderContents.ContainsKey(guid))
                {
                    folderContents.Remove(guid);
                }
            }
        }
        
        public static void UpdateCacheForFolder(string guid, string path)
        {
            if (folderContents.ContainsKey(guid)) return;
            
            var types = new HashSet<System.Type>();
            // Find all assets in this folder (non-recursive for performance)
            string[] childGuids = AssetDatabase.FindAssets("", new[] { path });
            
            foreach (var g in childGuids)
            {
                if (g == guid) continue;
                string p = AssetDatabase.GUIDToAssetPath(g);
                
                string parentPath = Path.GetDirectoryName(p).Replace("\\", "/");
                if (parentPath == path) // Immediate child
                {
                    System.Type type = AssetDatabase.GetMainAssetTypeAtPath(p);
                    if (type != null && type != typeof(DefaultAsset)) // Skip folders
                    {
                        types.Add(type);
                    }
                }
            }
            folderContents[guid] = types;
        }
    }
    
    public class FolderContentPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload) return;

            foreach (var path in importedAssets) FolderContentCache.MarkDirty(path);
            foreach (var path in deletedAssets) FolderContentCache.MarkDirty(path);
            foreach (var path in movedAssets) FolderContentCache.MarkDirty(path);
            foreach (var path in movedFromAssetPaths) FolderContentCache.MarkDirty(path);
            
            EditorApplication.RepaintProjectWindow();
        }
    }
}
