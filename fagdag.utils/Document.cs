using Pinecone;

namespace Fagdag.Utils;

public class Document(
    string id,
    float[] values,
    Pinecone.Metadata? metadata = null)
{
    public string Id { get; set; } = id;
    public float[] Values { get; set; } = values;
    public Pinecone.Metadata Metadata { get; set; } = metadata ?? new();
}

public static class DocumentExtensions
{
    public static Vector ToVector(this Document document)
    {
        return new()
        {
            Values = document.Values,
            Id = document.Id,
            Metadata = document.Metadata
        };
    }
}
