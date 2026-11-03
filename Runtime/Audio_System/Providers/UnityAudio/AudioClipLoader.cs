using System;
using System.Collections;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Data;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Providers.UnityAudio
{
    /// <summary>
    /// Loads and unloads clip data for <see cref="UnityAudioProvider"/>, reference-counted per clip
    /// because sounds can share clips. A clip that reaches zero references while it is still in use
    /// waits on a pending list and unloads once nothing is using it.
    ///
    /// <para>Loading only avoids blocking the main thread for clips with "Load In Background" ticked
    /// in their import settings. That can't be changed at runtime, so other clips log a warning.</para>
    /// </summary>
    internal sealed class AudioClipLoader
    {
        // Give up when no clip has settled for this long (e.g. LoadAudioData refused).
        private const float LoadStallTimeoutSeconds = 10f;

        private readonly MonoBehaviour _runner;
        private readonly Func<AudioClip, bool> _isClipInUse;
        private readonly Dictionary<AudioClip, int> _refCounts = new Dictionary<AudioClip, int>();
        private readonly List<AudioClip> _pendingUnload = new List<AudioClip>();

        internal AudioClipLoader(MonoBehaviour runner, Func<AudioClip, bool> isClipInUse)
        {
            _runner = runner;
            _isClipInUse = isClipInUse;
        }

        /// <summary>
        /// Adds a reference to every clip of the sounds and starts loading them. <paramref name="done"/>
        /// always fires once; if everything is already loaded, it fires before this returns.
        /// </summary>
        internal void Load(IReadOnlyList<SoundDefinition> sounds, Action<float> progress, Action done)
        {
            var clips = new List<AudioClip>();
            if (sounds != null)
            {
                for (int i = 0; i < sounds.Count; i++)
                {
                    var sound = sounds[i];
                    if (sound == null || sound.Clips == null) continue;
                    for (int c = 0; c < sound.Clips.Length; c++)
                    {
                        var clip = sound.Clips[c];
                        if (clip == null) continue;
                        _refCounts.TryGetValue(clip, out int count);
                        _refCounts[clip] = count + 1;
                        _pendingUnload.Remove(clip);
                        if (!clips.Contains(clip)) clips.Add(clip);
                    }
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip.loadType == AudioClipLoadType.Streaming || clip.loadState != AudioDataLoadState.Unloaded) continue;
                if (!clip.loadInBackground)
                    Debug.LogWarning($"[AudioClipLoader] Clip '{clip.name}' doesn't have Load In Background ticked, so loading it blocks the main thread.");
                clip.LoadAudioData();
            }

            _runner.StartCoroutine(WaitForClips(clips, progress, done));
        }

        private IEnumerator WaitForClips(List<AudioClip> clips, Action<float> progress, Action done)
        {
            int lastSettled = -1;
            float stalled = 0f;
            while (true)
            {
                int settled = CountSettled(clips);
                if (settled != lastSettled)
                {
                    lastSettled = settled;
                    stalled = 0f;
                    if (settled < clips.Count) progress?.Invoke((float)settled / clips.Count);
                }

                if (settled >= clips.Count) break;

                stalled += Time.unscaledDeltaTime;
                if (stalled >= LoadStallTimeoutSeconds)
                {
                    Debug.LogWarning($"[AudioClipLoader] Timed out with {clips.Count - settled} clip(s) still loading.");
                    break;
                }
                yield return null;
            }

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip != null && clip.loadState == AudioDataLoadState.Failed)
                    Debug.LogWarning($"[AudioClipLoader] Clip '{clip.name}' failed to load.");
            }

            progress?.Invoke(1f);
            done?.Invoke();
        }

        private int CountSettled(List<AudioClip> clips)
        {
            int settled = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                // A clip no longer wanted counts too: an Unload may have taken it back to Unloaded.
                if (clip == null || !_refCounts.ContainsKey(clip) || IsSettled(clip)) settled++;
            }
            return settled;
        }

        // Streaming clips never report Loaded, so they count as ready immediately.
        private static bool IsSettled(AudioClip clip)
        {
            if (clip.loadType == AudioClipLoadType.Streaming) return true;
            var state = clip.loadState;
            return state == AudioDataLoadState.Loaded || state == AudioDataLoadState.Failed;
        }

        /// <summary>Drops one reference per clip, matching <see cref="Load"/>. Clips that Load never counted are left alone.</summary>
        internal void Unload(IReadOnlyList<SoundDefinition> sounds)
        {
            if (sounds == null) return;
            for (int i = 0; i < sounds.Count; i++)
            {
                var sound = sounds[i];
                if (sound == null || sound.Clips == null) continue;
                for (int c = 0; c < sound.Clips.Length; c++)
                {
                    var clip = sound.Clips[c];
                    if (clip == null || !_refCounts.TryGetValue(clip, out int count)) continue;
                    if (count > 1)
                    {
                        _refCounts[clip] = count - 1;
                        continue;
                    }
                    _refCounts.Remove(clip);
                    _pendingUnload.Add(clip);
                }
            }
        }

        /// <summary>True when every clip of the sound is ready to play.</summary>
        internal static bool IsLoaded(SoundDefinition sound)
        {
            if (sound == null) return false;
            if (sound.Clips == null) return true;
            for (int i = 0; i < sound.Clips.Length; i++)
            {
                var clip = sound.Clips[i];
                if (clip == null || clip.loadType == AudioClipLoadType.Streaming) continue;
                if (clip.loadState != AudioDataLoadState.Loaded) return false;
            }
            return true;
        }

        /// <summary>Unloads pending clips that nothing is using any more. Call once per frame.</summary>
        internal void ProcessPendingUnloads()
        {
            for (int i = _pendingUnload.Count - 1; i >= 0; i--)
            {
                var clip = _pendingUnload[i];
                if (clip != null)
                {
                    if (clip.loadState == AudioDataLoadState.Loading || _isClipInUse(clip)) continue;
                    if (clip.loadType != AudioClipLoadType.Streaming && clip.loadState == AudioDataLoadState.Loaded)
                        clip.UnloadAudioData();
                }
                _pendingUnload.RemoveAt(i);
            }
        }
    }
}
