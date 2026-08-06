using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    [Serializable]
    public class BookmarkConfig
    {
        public string guid;
        public bool isBookmarked;
        public Color color = Color.clear;
        public string customIcon = "";
    }

    [Serializable]
    public class BookmarkProfile
    {
        public string profileName = "Default";
        public List<BookmarkConfig> bookmarks = new List<BookmarkConfig>();
    }

    [FilePath("ProjectSettings/ProjectWindowNavigatorData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ProjectBookmarkData : ScriptableSingleton<ProjectBookmarkData>
    {
        public List<BookmarkProfile> profiles = new List<BookmarkProfile>() { new BookmarkProfile() };
        public int activeProfileIndex = 0;

        public BookmarkProfile ActiveProfile
        {
            get
            {
                if (profiles == null || profiles.Count == 0)
                {
                    profiles = new List<BookmarkProfile>() { new BookmarkProfile() };
                    activeProfileIndex = 0;
                }
                if (activeProfileIndex < 0 || activeProfileIndex >= profiles.Count)
                {
                    activeProfileIndex = 0;
                }
                return profiles[activeProfileIndex];
            }
        }

        public void Save()
        {
            Save(true);
        }

        public BookmarkConfig GetConfig(string guid)
        {
            return ActiveProfile.bookmarks.Find(x => x.guid == guid);
        }

        public void ClearConfig(string guid)
        {
            var config = GetConfig(guid);
            if (config != null)
            {
                ActiveProfile.bookmarks.Remove(config);
                Save();
            }
        }
        
        public void SetBookmark(string guid, bool bookmark)
        {
            var config = GetConfig(guid);
            if (config == null)
            {
                if (!bookmark) return; // nothing to do
                config = new BookmarkConfig { guid = guid };
                ActiveProfile.bookmarks.Add(config);
            }
            config.isBookmarked = bookmark;
            Save();
        }

        public void ReorderBookmark(string guid, int newIndex)
        {
            var config = GetConfig(guid);
            if (config == null) return;
            
            var list = ActiveProfile.bookmarks;
            int oldIndex = list.IndexOf(config);
            if (oldIndex == -1 || oldIndex == newIndex) return;

            list.RemoveAt(oldIndex);
            
            // Adjust newIndex if it's out of bounds after removal
            if (newIndex > list.Count) newIndex = list.Count;
            if (newIndex < 0) newIndex = 0;

            list.Insert(newIndex, config);
            Save();
        }
    }
}
