using UnityEngine;
using UnityEditor;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    [FilePath("ProjectSettings/ProjectWindowEnhancerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ProjectWindowEnhancerSettings : ScriptableSingleton<ProjectWindowEnhancerSettings>
    {
        public bool enableCustomIcons = true;
        public bool enableCustomColors = true;
        public bool enableAutomaticIcons = true;
        public bool enableMinimalMode = false;

        public void Save()
        {
            Save(true);
        }
    }
}
