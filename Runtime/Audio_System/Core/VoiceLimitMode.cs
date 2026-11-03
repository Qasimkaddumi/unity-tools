namespace Kaddumi.UnityTools.Audio.Core
{
    /// <summary>What a sound does when it is already playing on its <c>MaxVoices</c> limit.</summary>
    public enum VoiceLimitMode
    {
        /// <summary>Stop the oldest voice of this sound and play the new one.</summary>
        StealOldest,

        /// <summary>Keep the voices that are playing and return an invalid handle for the new one.</summary>
        Refuse,
    }
}
