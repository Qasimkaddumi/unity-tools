using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Data
{
    /// <summary>
    /// ScriptableObject catalog mapping stable string IDs to <see cref="SoundDefinition"/>s.
    /// Assign one asset in the AudioManager inspector; game code then plays sounds by ID
    /// (e.g. <c>AudioManager.Instance.PlaySfx("ui.click")</c>) instead of holding clip refs.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Kaddumi/Audio/SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Tooltip("Sound assets this library exposes for string-ID playback. Each is looked up by its " +
                 "Id (or asset name when the Id is blank), which must be unique within this library.")]
        [SerializeField] private List<SoundDefinition> sounds = new List<SoundDefinition>();

        private Dictionary<string, SoundDefinition> _lookup;

        /// <summary>Attempts to resolve a sound by its <see cref="SoundDefinition.Id"/>.</summary>
        public bool TryGet(string id, out SoundDefinition def)
        {
            EnsureLookup();
            if (string.IsNullOrEmpty(id))
            {
                def = null;
                return false;
            }
            return _lookup.TryGetValue(id, out def);
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, SoundDefinition>(sounds.Count);
            foreach (var s in sounds)
            {
                if (s == null) continue;
                var id = s.ResolvedId;
                if (string.IsNullOrEmpty(id)) continue;
                _lookup[id] = s; // last one wins on duplicates; OnValidate warns about them
            }
        }

        // Rebuild lazily after edits in the inspector.
        private void OnDisable() => _lookup = null;

#if UNITY_EDITOR
        private void OnValidate()
        {
            _lookup = null;
            var seen = new HashSet<string>();
            foreach (var s in sounds)
            {
                if (s == null) continue;
                var id = s.ResolvedId;
                if (string.IsNullOrEmpty(id)) continue;
                if (!seen.Add(id))
                {
                    Debug.LogWarning($"[SoundLibrary] '{name}' has a duplicate sound Id '{id}'. " +
                                     "Only the last entry with this Id will be playable.", this);
                }
            }
        }
#endif
    }
}
