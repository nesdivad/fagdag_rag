using Pinecone;

namespace Fagdag.Utils;

public class Document(
    string id,
    string content,
    float[]? values = null,
    Metadata? metadata = null)
{
    public string Id { get; set; } = id;
    public string Content { get; set; } = content;
    public float[]? Values { get; set; } = values;
    public Metadata Metadata { get; set; } = metadata ?? [];
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
