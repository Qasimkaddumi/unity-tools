namespace Kaddumi.UnityTools.Api.Interfaces
{
    /// <summary>
    /// Converts request/response bodies to and from their wire text (JSON). Abstracted
    /// so the API layer does not hard-depend on one JSON library: the default is
    /// <see cref="Serialization.UnityJsonSerializer"/> (Unity's <c>JsonUtility</c>, matching
    /// the rest of the project), but a Newtonsoft-based one can be dropped in without
    /// touching the client or any repository.
    /// </summary>
    public interface IApiSerializer
    {
        /// <summary>Serializes <paramref name="value"/> to its wire representation.</summary>
        string Serialize(object value);

        /// <summary>
        /// Deserializes <paramref name="text"/> into <typeparamref name="T"/>. Returns
        /// <c>default</c> when the text is null/empty.
        /// </summary>
        T Deserialize<T>(string text);

        /// <summary>The Content-Type this serializer produces (e.g. <c>application/json</c>).</summary>
        string ContentType { get; }
    }
}
