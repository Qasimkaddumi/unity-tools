using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Timeline;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace Kaddumi.UnityTools.Audio.Timeline.Editor
{
    /// <summary>
    /// Draws a waveform preview on <see cref="GameAudioClip"/> clips in the Timeline window,
    /// mirroring the look of Unity's built-in audio clips. The waveform respects the clip's
    /// clip-in offset and speed multiplier, and follows zoom/scroll via the region's time span.
    /// Editor-only; discovered automatically through <see cref="CustomTimelineEditorAttribute"/>.
    /// </summary>
    [CustomTimelineEditor(typeof(GameAudioClip))]
    public class GameAudioClipEditor : ClipEditor
    {
        // Fixed-resolution min/max amplitude envelope, computed once per AudioClip and reused
        // across repaints (rebuilding from GetData every frame would be far too expensive).
        private class Envelope
        {
            public float[] Min;
            public float[] Max;
        }

        private const int Resolution = 4096;
        private static readonly Dictionary<AudioClip, Envelope> Cache = new Dictionary<AudioClip, Envelope>();
        private static readonly Color WaveColor = new Color(0.09f, 0.32f, 0.46f, 0.9f);

        public override void DrawBackground(TimelineClip clip, ClipBackgroundRegion region)
        {
            if (Event.current.type != EventType.Repaint) return;

            AudioClip source = GetAudioClip(clip);
            if (source == null || source.length <= 0f) return;

            Envelope env = GetEnvelope(source);
            if (env == null) return;

            Rect rect = region.position;
            if (rect.width < 1f || rect.height < 1f) return;

            double clipIn = clip.clipIn;
            double timeScale = clip.timeScale <= 0.0 ? 1.0 : clip.timeScale;
            double audioLen = source.length;
            int res = env.Min.Length;

            float mid = rect.y + rect.height * 0.5f;
            float half = rect.height * 0.5f * 0.9f;

            // One thin vertical bar per pixel column, spanning the min..max sample in that column.
            for (float x = rect.xMin; x < rect.xMax; x += 1f)
            {
                double localA = Lerp(region.startTime, region.endTime, (x - rect.xMin) / rect.width);
                double localB = Lerp(region.startTime, region.endTime, (x + 1f - rect.xMin) / rect.width);

                // Clip-local timeline time -> source media time (offset by clip-in, scaled by speed).
                double srcA = clipIn + localA * timeScale;
                double srcB = clipIn + localB * timeScale;

                // Past the end of the source audio there's nothing to draw (viz ignores looping).
                if (srcA >= audioLen) continue;
                srcB = Math.Min(srcB, audioLen);

                int i0 = Mathf.Clamp((int)(srcA / audioLen * res), 0, res - 1);
                int i1 = Mathf.Clamp((int)(srcB / audioLen * res), 0, res - 1);
                if (i1 < i0) i1 = i0;

                float mn = 1f, mx = -1f;
                for (int i = i0; i <= i1; i++)
                {
                    if (env.Min[i] < mn) mn = env.Min[i];
                    if (env.Max[i] > mx) mx = env.Max[i];
                }
                if (mx < mn) continue;

                float yTop = mid - mx * half;
                float yBot = mid - mn * half;
                EditorGUI.DrawRect(new Rect(x, yTop, 1f, Mathf.Max(1f, yBot - yTop)), WaveColor);
            }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static AudioClip GetAudioClip(TimelineClip clip)
        {
            var asset = clip != null ? clip.asset as GameAudioClip : null;
            var sound = asset != null ? asset.sound : null;
            if (sound == null || sound.Clips == null) return null;
            for (int i = 0; i < sound.Clips.Length; i++)
                if (sound.Clips[i] != null) return sound.Clips[i];
            return null;
        }

        private static Envelope GetEnvelope(AudioClip clip)
        {
            if (Cache.TryGetValue(clip, out var cached)) return cached;
            // Cache the result (including null on a failed read) so we don't re-decode each repaint.
            var env = BuildEnvelope(clip);
            Cache[clip] = env;
            return env;
        }

        private static Envelope BuildEnvelope(AudioClip clip)
        {
            int channels = clip.channels;
            int frames = clip.samples;
            if (channels <= 0 || frames <= 0) return null;

            int res = Mathf.Clamp(frames, 1, Resolution);
            var min = new float[res];
            var max = new float[res];
            for (int i = 0; i < res; i++) { min[i] = 1f; max[i] = -1f; }

            // Read in chunks so we never allocate the whole clip at once (long clips are huge).
            const int chunkFrames = 65536;
            var buffer = new float[chunkFrames * channels];

            int frame = 0;
            while (frame < frames)
            {
                int count = Mathf.Min(chunkFrames, frames - frame);
                float[] target = count == chunkFrames ? buffer : new float[count * channels];
                // GetData returns false for streaming/compressed-in-memory clips we can't read.
                if (!clip.GetData(target, frame)) return null;

                for (int f = 0; f < count; f++)
                {
                    // Average channels so stereo collapses to one combined envelope.
                    float sum = 0f;
                    int baseIdx = f * channels;
                    for (int c = 0; c < channels; c++) sum += target[baseIdx + c];
                    float v = sum / channels;

                    int bucket = (int)((long)(frame + f) * res / frames);
                    if (bucket >= res) bucket = res - 1;
                    if (v < min[bucket]) min[bucket] = v;
                    if (v > max[bucket]) max[bucket] = v;
                }
                frame += count;
            }

            // Any bucket that never received a sample (rare) renders as silence.
            for (int i = 0; i < res; i++)
                if (max[i] < min[i]) { min[i] = 0f; max[i] = 0f; }

            return new Envelope { Min = min, Max = max };
        }
    }
}
