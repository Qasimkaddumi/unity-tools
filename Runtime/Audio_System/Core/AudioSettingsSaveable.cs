using System;
using System.Collections.Generic;
using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Audio.Core
{
    /// <summary>
    /// Bridges the audio system's per-bus volume/mute settings into the existing Save system.
    /// Owned by <see cref="AudioManager"/> and registered with the SaveManager, so audio settings
    /// ride along with every save slot. Plain object (not a MonoBehaviour) implementing
    /// <see cref="ISaveable"/> — it delegates capture/apply back to the manager.
    /// </summary>
    internal class AudioSettingsSaveable : ISaveable
    {
        public const string Key = "audio.settings";

        private readonly Func<AudioSettingsState> _capture;
        private readonly Action<AudioSettingsState> _apply;

        public AudioSettingsSaveable(Func<AudioSettingsState> capture, Action<AudioSettingsState> apply)
        {
            _capture = capture;
            _apply = apply;
        }

        public string SaveKey => Key;

        public string CaptureState() => JsonUtility.ToJson(_capture());

        public void RestoreState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            var parsed = JsonUtility.FromJson<AudioSettingsState>(state);
            if (parsed != null) _apply(parsed);
        }
    }

    /// <summary>
    /// Serializable snapshot of the audio settings. Uses parallel arrays because
    /// <see cref="JsonUtility"/> can't serialize dictionaries.
    /// </summary>
    [Serializable]
    internal class AudioSettingsState
    {
        public List<BusSetting> Buses = new List<BusSetting>();

        [Serializable]
        public class BusSetting
        {
            public AudioBus Bus;
            public float Volume;
            public bool Muted;
        }
    }
}
