using Kaddumi.UnityTools.Audio.Core;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Data
{
    /// <summary>
    /// A single playable sound, authored as its own ScriptableObject asset so designers can add,
    /// edit, and reuse sounds without touching code or a monolithic list. Create one via
    /// <b>Assets ▸ Create ▸ Kaddumi ▸ Audio ▸ Sound</b>.
    ///
    /// <para>Play a sound either by dropping the asset straight into a component and calling
    /// <c>AudioManager.PlaySfx(soundAsset)</c>, or by listing it in a <see cref="SoundLibrary"/>
    /// and playing it by its <see cref="Id"/>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Sound", menuName = "Kaddumi/Audio/Sound")]
    public class SoundDefinition : ScriptableObject
    {
        [Tooltip("Stable, unique identifier used to play this sound by string (e.g. \"ui.click\", " +
                 "\"music.theme\") when it's registered in a SoundLibrary. Leave blank to default to the " +
                 "asset name. Only needs to be unique within a given library.")]
        public string Id;

        [Tooltip("One or more clips. When more than one is set, a random clip is chosen each play " +
                 "(useful for footsteps/impacts to avoid the 'machine-gun' effect).")]
        public AudioClip[] Clips;

        [Tooltip("Mixing bus this sound is routed through.")]
        public AudioBus Bus = AudioBus.SFX;

        [Tooltip("Per-sound base volume, multiplied on top of the bus volume.")]
        [Range(0f, 1f)] public float Volume = 1f;

        [Tooltip("Random pitch range applied per play (x = min, y = max). Set both to 1 for no variation.")]
        public Vector2 PitchRange = new Vector2(1f, 1f);

        [Tooltip("Loop the clip. Looping sounds keep their voice until explicitly stopped.")]
        public bool Loop = false;

        [Tooltip("0 = fully 2D (positionless), 1 = fully 3D (attenuates with distance from the listener).")]
        [Range(0f, 1f)] public float SpatialBlend = 0f;

        [Tooltip("3D min distance: below this the sound plays at full volume. Ignored when SpatialBlend is 0.")]
        [Min(0f)] public float MinDistance = 1f;

        [Tooltip("3D max distance: beyond this the sound stops attenuating. Ignored when SpatialBlend is 0.")]
        [Min(0f)] public float MaxDistance = 50f;

        [Tooltip("Maximum simultaneous voices for this sound. When exceeded, the oldest voice is stolen. " +
                 "0 = unlimited.")]
        [Min(0)] public int MaxVoices = 0;

        [Tooltip("AudioSource priority (0 = most important, 256 = least). Lower survives Unity's own voice limiting.")]
        [Range(0, 256)] public int Priority = 128;

        /// <summary>
        /// The identifier used for library lookups: the explicit <see cref="Id"/> when set,
        /// otherwise the asset name. Lets designers add a sound by simply naming the asset.
        /// </summary>
        public string ResolvedId => string.IsNullOrEmpty(Id) ? name : Id;

        /// <summary>Picks a clip to play — random when several are configured, else the single clip.</summary>
        public AudioClip PickClip()
        {
            if (Clips == null || Clips.Length == 0) return null;
            if (Clips.Length == 1) return Clips[0];
            return Clips[Random.Range(0, Clips.Length)];
        }

        /// <summary>Resolves a concrete pitch for this play from the configured range.</summary>
        public float PickPitch()
        {
            if (Mathf.Approximately(PitchRange.x, PitchRange.y)) return PitchRange.x;
            return Random.Range(PitchRange.x, PitchRange.y);
        }
    }
}
