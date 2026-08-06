using Kaddumi.UnityTools.ToolManager.Editor;

using UnityEditor;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public class ProjectWindowNavigatorModule : IEditorToolModule
    {
        public string Id => "project-window-navigator";
        public string DisplayName => "Project Window Navigator";
        public string Description => "Adds a navigation bar with back/forward history and folder bookmarks.";
        public string Category => "UI Enhancements";
        public bool DefaultEnabled => true;

        public void OnActivated()
        {
            ProjectWindowHover.Enable();
            ProjectWindowNavigationBar.Enable();
            ProjectWindowShortcuts.Enable();
        }

        public void OnDeactivated()
        {
            ProjectWindowHover.Disable();
            ProjectWindowNavigationBar.Disable();
            ProjectWindowShortcuts.Disable();
        }
    }
}
