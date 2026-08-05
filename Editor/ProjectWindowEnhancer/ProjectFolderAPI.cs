using UnityEngine;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    /// <summary>
    /// Simple API to customize folders from code.
    /// </summary>
    public static class ProjectFolderAPI
    {
        public static void SetFolderColor(string guid, Color color)
        {
            var config = ProjectFolderData.instance.GetConfig(guid);
            if (config != null)
            {
                ProjectFolderData.instance.SetConfig(guid, color, config.iconName);
            }
            else
            {
                ProjectFolderData.instance.SetConfig(guid, color, "");
            }
        }
        
        public static void SetFolderIcon(string guid, string iconName)
        {
            var config = ProjectFolderData.instance.GetConfig(guid);
            if (config != null)
            {
                ProjectFolderData.instance.SetConfig(guid, config.color, iconName);
            }
            else
            {
                ProjectFolderData.instance.SetConfig(guid, Color.clear, iconName);
            }
        }
        
        public static void SetFolderCustomization(string guid, Color color, string iconName)
        {
            ProjectFolderData.instance.SetConfig(guid, color, iconName);
        }
        
        public static void ClearFolderCustomization(string guid)
        {
            ProjectFolderData.instance.ClearConfig(guid);
        }
    }
}
