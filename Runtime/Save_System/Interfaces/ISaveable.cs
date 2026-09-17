namespace Kaddumi.UnityTools.Save.Interfaces
{
    /// <summary>
    /// Contract for any object whose state should be captured into a save and restored
    /// from one. Each saveable owns a stable <see cref="SaveKey"/>, names the store its
    /// data belongs in via <see cref="StorageId"/>, and serializes itself to/from a JSON
    /// string — keeping the save system agnostic of concrete data shapes.
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

        /// <summary>
        /// Id of the save store this object's data lives in — <c>SaveStores.Local</c> for
        /// settings and device-bound state, <c>SaveStores.Cloud</c> for progression that
        /// should follow the player, or any custom id bound in the SaveManager inspector.
        /// Null or empty routes to the first (default) store.
        ///
        /// <para>Changing this for an existing saveable strands whatever is already written
        /// in the old store: a load only restores keys from the store they now point at.</para>
        /// </summary>
        string StorageId { get; }

        /// <summary>Serializes the current state to a JSON string.</summary>
        string CaptureState();

        /// <summary>Restores state from a JSON string produced by <see cref="CaptureState"/>.</summary>
        void RestoreState(string state);
    }
}
