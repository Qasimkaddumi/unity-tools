using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    [Serializable]
    public class FolderConfig
    {
        public string guid;
        public bool isBookmarked;
    }

    [FilePath("ProjectSettings/ProjectWindowNavigatorData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ProjectBookmarkData : ScriptableSingleton<ProjectBookmarkData>
    {
        public List<FolderConfig> folderConfigs = new List<FolderConfig>();

        public void Save()
        {
            Save(true);
        }

        public FolderConfig GetConfig(string guid)
        {
            return folderConfigs.Find(x => x.guid == guid);
        }

        public void ClearConfig(string guid)
        {
            var config = GetConfig(guid);
            if (config != null)
            {
                folderConfigs.Remove(config);
                Save();
            }
        }
        
        public void SetBookmark(string guid, bool bookmark)
        {
            var config = GetConfig(guid);
            if (config == null)
            {
                if (!bookmark) return; // nothing to do
                config = new FolderConfig { guid = guid };
                folderConfigs.Add(config);
            }
            config.isBookmarked = bookmark;
            Save();
        }
    }
}
