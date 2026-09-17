using Kaddumi.UnityTools.Save.Core;
using System;

namespace Kaddumi.UnityTools.Save.Interfaces
{
    /// <summary>
    /// Optional capability an <see cref="ISaveProvider"/> can add to store raw bytes
    /// natively — screenshots, thumbnails, recordings, anything that isn't text.
    ///
    /// <para>It is deliberately separate from <see cref="ISaveProvider"/> so existing and
    /// third-party providers keep compiling: <c>SaveService</c> checks for this interface
    /// and, when a provider doesn't implement it, falls back to base64 over the string
    /// channel. That fallback is correct everywhere but costs roughly a third more bytes
    /// and an extra copy of the payload, so backends that can do better — anything writing
    /// to a file or a blob store — should implement this.</para>
    ///
    /// <para>Blob keys share the provider's key space with slot payloads, so a provider
    /// needs no extra bookkeeping: <see cref="ISaveProvider.Delete"/>,
    /// <see cref="ISaveProvider.Exists"/> and <see cref="ISaveProvider.List"/> already work
    /// on them.</para>
    /// </summary>
    public interface ISaveBlobProvider
    {
        /// <summary>Persists <paramref name="data"/> under <paramref name="key"/>, overwriting any existing value.</summary>
        void WriteBytes(string key, byte[] data, Action<SaveResult> onComplete);

        /// <summary>Reads the bytes for <paramref name="key"/>. On success <c>SaveBlobResult.Data</c> is populated.</summary>
        void ReadBytes(string key, Action<SaveBlobResult> onComplete);
    }
}
