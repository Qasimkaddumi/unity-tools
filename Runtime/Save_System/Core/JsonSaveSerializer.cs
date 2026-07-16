using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// Default <see cref="ISaveSerializer"/> built on Unity's <see cref="JsonUtility"/>.
    /// Zero external dependencies (mirrors how the debug providers avoid SDKs), at the
    /// cost of <see cref="JsonUtility"/>'s limitations — it serializes public/[SerializeField]
    /// fields, not properties or dictionaries. <see cref="SaveData"/> is shaped to fit.
    /// </summary>
    public class JsonSaveSerializer : ISaveSerializer
    {
        private readonly bool prettyPrint;

        public JsonSaveSerializer(bool prettyPrint = false)
        {
            this.prettyPrint = prettyPrint;
        }

        public string Serialize(SaveData data) => JsonUtility.ToJson(data, prettyPrint);

        public SaveData Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(raw);
            }
            catch
            {
                // Malformed payload — caller treats null as Corrupted.
                return null;
            }
        }
    }
}
