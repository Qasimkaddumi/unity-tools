using Kaddumi.UnityTools.Save.Core;

namespace Kaddumi.UnityTools.Save.Interfaces
{
    /// <summary>
    /// Converts a <see cref="SaveData"/> container to and from a string payload. Kept
    /// separate from the storage backend so the on-disk format (plain JSON, pretty JSON,
    /// or a future binary format) can change independently of where the bytes live.
    /// </summary>
    public interface ISaveSerializer
    {
        string Serialize(SaveData data);

        /// <summary>
        /// Parses a payload back into a <see cref="SaveData"/>. Implementations should
        /// return null (never throw) when the input is malformed so callers can surface a
        /// <see cref="SaveErrorType.Corrupted"/> error.
        /// </summary>
        SaveData Deserialize(string raw);
    }
}
