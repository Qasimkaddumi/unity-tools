using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Core
{
    /// <summary>
    /// Per-play overrides for <c>AudioManager.PlaySfxWithOptions</c> and <c>PlaySfxScheduled</c>.
    /// Anything left unset falls back to the <see cref="Data.SoundDefinition"/>, so the asset is
    /// never copied and a play call doesn't allocate.
    ///
    /// <para><see cref="Spatial"/> and <see cref="Follow"/> are independent: Spatial decides whether
    /// the sound is 3D, Follow decides whether its position tracks an object. A moving engine loop
    /// needs both.</para>
    /// </summary>
    public struct SoundPlayOptions
    {
        /// <summary>Clip to play instead of one picked from the definition. Null uses <c>PickClip()</c>.</summary>
        public AudioClip ClipOverride;

        /// <summary>Linear volume for this play. Null uses the definition's volume.</summary>
        public float? Volume;

        /// <summary>Pitch for this play. Null picks from the definition's pitch range.</summary>
        public float? Pitch;

        /// <summary>Looping for this play. Null uses the definition's setting.</summary>
        public bool? Loop;

        /// <summary>World position of a spatial sound. Ignored when <see cref="Follow"/> is set.</summary>
        public Vector3 Position;

        /// <summary>
        /// Object whose position the voice tracks. Stays attached until the voice ends. If the target
        /// is destroyed, a looping voice stops and a one-shot finishes at its last position.
        /// </summary>
        public Transform Follow;

        /// <summary>Play in 3D. Only takes effect if the definition's SpatialBlend is greater than 0.</summary>
        public bool Spatial;
    }
}
