using UnityEngine;

namespace Kaddumi.UnityTools.Save.Data
{
    /// <summary>
    /// Shared configuration for the save system. Assign one asset in the SaveManager
    /// inspector. Mirrors <c>AuthConfig</c>/<c>AdConfig</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveConfig", menuName = "Kaddumi/Save/SaveConfig")]
    public class SaveConfig : ScriptableObject
    {
        [Header("Format")]
        [Tooltip("Current save-format version. Stamped into every save; loading a save with a " +
                 "higher version fails so old builds don't misread newer data. Bump when the shape changes.")]
        public int SaveVersion = 1;

        [Tooltip("Pretty-print the JSON payload. Handy while debugging; turn off for smaller saves.")]
        public bool PrettyPrint = false;

        [Header("Slots")]
        [Tooltip("Number of save slots the game exposes. Slot indices are 0..SlotCount-1.")]
        [Min(1)] public int SlotCount = 3;

        [Tooltip("Slot used by quick/auto saves and the parameterless Save()/Load() helpers.")]
        [Min(0)] public int DefaultSlot = 0;

        [Header("Auto Save")]
        [Tooltip("Periodically save the DefaultSlot in the background while playing.")]
        public bool AutoSave = false;

        [Tooltip("Seconds between auto saves.")]
        [Min(1f)] public float AutoSaveIntervalSeconds = 60f;

        [Header("Lifecycle Saves")]
        [Tooltip("Save the DefaultSlot when the application is quitting.")]
        public bool SaveOnQuit = true;

        [Tooltip("Save the DefaultSlot when the app is backgrounded (recommended on mobile, " +
                 "where OnApplicationQuit is not guaranteed to fire).")]
        public bool SaveOnPause = true;

        [Header("Playtime")]
        [Tooltip("Accumulate elapsed play time and store it in each save's metadata.")]
        public bool TrackPlaytime = true;
    }
}
