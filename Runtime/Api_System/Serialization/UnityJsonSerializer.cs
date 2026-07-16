using Kaddumi.UnityTools.Api.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Api.Serialization
{
    /// <summary>
    /// Default <see cref="IApiSerializer"/> built on Unity's <see cref="JsonUtility"/>,
    /// matching how the rest of the project serializes (e.g. <c>AvatarConfiguration</c>).
    /// It needs no extra packages. Its limits are <c>JsonUtility</c>'s: it maps
    /// <c>[Serializable]</c> classes/structs with fields, and cannot represent a
    /// top-level array, dictionary, or bare primitive — wrap those in a container type.
    /// Swap in a Newtonsoft-based serializer if you need that.
    /// </summary>
    public sealed class UnityJsonSerializer : IApiSerializer
    {
        public string ContentType => "application/json";

        public string Serialize(object value) => value == null ? null : JsonUtility.ToJson(value);

        public T Deserialize<T>(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return default;
            return JsonUtility.FromJson<T>(text);
        }
    }
}
