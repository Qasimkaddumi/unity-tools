namespace Kaddumi.UnityTools.ToolManager.Editor
{
    /// <summary>
    /// Optional interface for <see cref="IEditorToolModule"/> implementations.
    /// If a tool implements this, the Tool Manager window will display an inline GUI
    /// for its settings.
    /// </summary>
    public interface IHasEditorToolSettings
    {
        /// <summary>
        /// Called to draw the tool's custom settings GUI inside the Tool Manager window.
        /// </summary>
        void DrawSettings();
    }
}
