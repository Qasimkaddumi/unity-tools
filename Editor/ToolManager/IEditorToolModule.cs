namespace Kaddumi.UnityTools.ToolManager.Editor
{
    /// <summary>
    /// Contract for a self-contained editor feature that can be turned on and off from the
    /// <see cref="EditorToolManagerWindow"/>. Implement this on a class with a public parameterless
    /// constructor and it is discovered, listed, and lifecycle-managed automatically — no manual
    /// registration required.
    /// </summary>
    public interface IEditorToolModule
    {
        /// <summary>
        /// Stable, unique identifier. Used as the root of this tool's <c>EditorPrefs</c> key, so it
        /// must not change once shipped or the saved on/off state is lost.
        /// </summary>
        string Id { get; }

        /// <summary> Human-readable name shown in the manager window. </summary>
        string DisplayName { get; }

        /// <summary> One-line explanation of what the tool does, shown under its name. </summary>
        string Description { get; }

        /// <summary>
        /// Optional grouping label. Tools sharing a category are drawn together. Return an empty
        /// string for the default "General" group.
        /// </summary>
        string Category { get; }

        /// <summary> Whether the tool is on the first time it is seen (no saved preference yet). </summary>
        bool DefaultEnabled { get; }

        /// <summary>
        /// Called when the tool becomes active — either the user enabled it, or it was already
        /// enabled when the editor loaded. Subscribe to editor callbacks / allocate resources here.
        /// </summary>
        void OnActivated();

        /// <summary>
        /// Called when the tool is turned off. Unsubscribe from callbacks and release anything
        /// allocated in <see cref="OnActivated"/> here.
        /// </summary>
        void OnDeactivated();
    }
}
