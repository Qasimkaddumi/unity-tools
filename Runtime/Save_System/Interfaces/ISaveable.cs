namespace Kaddumi.UnityTools.Save.Interfaces
{
    /// <summary>
    /// Contract for any object whose state should be captured into a save and restored
    /// from one. Each saveable owns a stable <see cref="SaveKey"/> and serializes itself
    /// to/from a JSON string, keeping the save system agnostic of concrete data shapes.
    ///
    /// Most gameplay code should derive from <c>SaveableBehaviour&lt;TState&gt;</c> instead
    /// of implementing this directly — it handles the JSON conversion and registration.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Stable, unique identifier for this object's slice of the save. Must remain
        /// constant across sessions/builds or its data will not be restored.
        /// </summary>
        string SaveKey { get; }

        /// <summary>Serializes the current state to a JSON string.</summary>
        string CaptureState();

        /// <summary>Restores state from a JSON string produced by <see cref="CaptureState"/>.</summary>
        void RestoreState(string state);
    }
}
