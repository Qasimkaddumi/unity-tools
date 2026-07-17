namespace Kaddumi.UnityTools.Audio.Core
{
    /// <summary>
    /// Logical mixing buses exposed by the audio system. Each value maps to an
    /// <c>AudioMixerGroup</c> and an exposed volume parameter configured in
    /// <see cref="Data.AudioConfig"/>. <see cref="Master"/> sits above the rest.
    /// </summary>
    public enum AudioBus
    {
        Master = 0,
        Music = 1,
        SFX = 2,
        UI = 3,
        Ambience = 4,
        Voice = 5,
    }
}
