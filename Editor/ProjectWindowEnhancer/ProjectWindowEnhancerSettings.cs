using UnityEngine;
using UnityEditor;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    [FilePath("ProjectSettings/ProjectWindowEnhancerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ProjectWindowEnhancerSettings : ScriptableSingleton<ProjectWindowEnhancerSettings>
    {
        public bool enableCustomIcons = true;
        public bool enableCustomColors = true;
        public bool enableContentMinimap = true;

        public void Save()
        {
            Save(true);
        }
    }
}
