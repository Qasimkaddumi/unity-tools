using Kaddumi.UnityTools.ToolManager.Editor;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public class ProjectWindowEnhancerModule : IEditorToolModule
    {
        public string Id => "project-window-enhancer";
        public string DisplayName => "Project Window Enhancer";
        public string Description => "Custom folder icons, colors, minimal mode, and a navigation bar.";
        public string Category => "UI Enhancements";
        public bool DefaultEnabled => true;

        public void OnActivated()
        {
            ProjectWindowEnhancer.Enable();
            ProjectWindowNavigationBar.Enable();
            ProjectWindowShortcuts.Enable();
        }

        public void OnDeactivated()
        {
            ProjectWindowEnhancer.Disable();
            ProjectWindowNavigationBar.Disable();
            ProjectWindowShortcuts.Disable();
        }
    }
}
