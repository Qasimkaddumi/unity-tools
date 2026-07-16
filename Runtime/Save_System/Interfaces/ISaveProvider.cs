using System;
using Kaddumi.UnityTools.Save.Core;

namespace Kaddumi.UnityTools.Save.Interfaces
{
    /// <summary>
    /// Storage backend contract: knows only how to read/write/delete an opaque string
    /// payload for a given key. Serialization is a separate concern
    /// (<see cref="ISaveSerializer"/>), so a provider can be PlayerPrefs, a JSON file,
    /// an encrypted file, or a cloud bucket without knowing what the bytes mean.
    ///
    /// The <see cref="Core.SaveService"/> routes every persistence call through the single
    /// active provider chosen in the SaveManager inspector.
    /// </summary>
    public interface ISaveProvider
    {
        bool IsInitialized { get; }

        void Initialize(Action onComplete);

        /// <summary>Persists <paramref name="data"/> under <paramref name="key"/>, overwriting any existing value.</summary>
        void Write(string key, string data, Action<SaveResult> onComplete);

        /// <summary>Reads the payload for <paramref name="key"/>. On success <c>SaveResult.Data</c> is populated.</summary>
        void Read(string key, Action<SaveResult> onComplete);

        void Delete(string key, Action<SaveResult> onComplete);

        void Exists(string key, Action<bool> onComplete);

        /// <summary>Returns the keys of every payload this provider currently holds.</summary>
        void List(Action<string[]> onComplete);
    }
}
