using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Data
{
    /// <summary>
    /// A set of music stems (layers) that play in sample-accurate sync, authored as its own
    /// ScriptableObject asset. Each stem fades independently while all keep playing, so game
    /// code can bring drums, strings, etc. in and out of one continuous track. Create one via
    /// <b>Assets ▸ Create ▸ Kaddumi ▸ Audio ▸ Music Stem Set</b>.
    ///
    /// <para><b>Requirements:</b> every stem must be exported from the same session with the same
    /// length and sample rate, otherwise the layers drift apart when they loop. Recommended
    /// import settings: <b>Vorbis</b>, <b>Compressed In Memory</b>, and <b>not Streaming</b>
    /// (several streamed clips starting together can stall and lose sync).</para>
    ///
    /// <para>Play a set either by passing the asset to <c>AudioManager.PlayStems(set)</c>, or by
    /// listing it in a <see cref="SoundLibrary"/> and playing it by its <see cref="Id"/>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MusicStemSet", menuName = "Kaddumi/Audio/Music Stem Set")]
    public class MusicStemSet : ScriptableObject
    {
        [Tooltip("Stable, unique identifier used to play this set by string when it's registered in a " +
                 "SoundLibrary. Leave blank to default to the asset name.")]
        public string Id;

        [Tooltip("The layers of this track. All clips must have the same length and sample rate. " +
                 "Recommended import: Vorbis, Compressed In Memory, not Streaming.")]
        public Stem[] Stems;

        [Tooltip("Master volume for the whole set, multiplied on top of each stem's level and the Music bus.")]
        [Range(0f, 1f)] public float Volume = 1f;

        /// <summary>
        /// The identifier used for library lookups: the explicit <see cref="Id"/> when set,
        /// otherwise the asset name.
        /// </summary>
        public string ResolvedId => string.IsNullOrEmpty(Id) ? name : Id;

        /// <summary>One layer of a <see cref="MusicStemSet"/>.</summary>
        [Serializable]
        public class Stem
        {
            [Tooltip("Identifier used to address this layer (e.g. \"drums\") from SetStemVolume. Must be unique within the set.")]
            public string Id;

            [Tooltip("The layer's clip. Must match the other stems' length and sample rate.")]
            public AudioClip Clip;

            [Tooltip("Linear volume (0..1) this layer starts at when the set begins playing. Use 0 for layers that fade in later.")]
            [Range(0f, 1f)] public float StartVolume = 1f;
        }

        /// <summary>Index of the stem with the given Id, or -1 if it isn't in this set.</summary>
        public int IndexOf(string stemId)
        {
            if (Stems == null || string.IsNullOrEmpty(stemId)) return -1;
            for (int i = 0; i < Stems.Length; i++)
            {
                if (Stems[i] != null && Stems[i].Id == stemId) return i;
            }
            return -1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Stems == null) return;

            var seen = new HashSet<string>();
            AudioClip reference = null;
            for (int i = 0; i < Stems.Length; i++)
            {
                var stem = Stems[i];
                if (stem == null) continue;

                if (string.IsNullOrEmpty(stem.Id))
                {
                    Debug.LogWarning($"[MusicStemSet] '{name}' stem #{i} has a blank Id and can't be addressed by SetStemVolume.", this);
                }
                else if (!seen.Add(stem.Id))
                {
                    Debug.LogWarning($"[MusicStemSet] '{name}' has a duplicate stem Id '{stem.Id}'. " +
                                     "Only the first stem with this Id can be addressed.", this);
                }

                if (stem.Clip == null)
                {
                    Debug.LogWarning($"[MusicStemSet] '{name}' stem #{i} ('{stem.Id}') has no clip assigned.", this);
                    continue;
                }

                if (reference == null)
                {
                    reference = stem.Clip;
                }
                else if (stem.Clip.samples != reference.samples || stem.Clip.frequency != reference.frequency)
                {
                    Debug.LogWarning($"[MusicStemSet] '{name}' stem '{stem.Id}' differs from the first stem's clip " +
                                     $"({stem.Clip.samples} samples @ {stem.Clip.frequency} Hz vs " +
                                     $"{reference.samples} samples @ {reference.frequency} Hz). Layers will drift out of sync.", this);
                }
            }
        }
#endif
    }
}
