using System.Text.Json.Serialization;

using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Fagdag.Utils;

public class Index
{
    [SearchableField(IsSortable = true, IsKey = true, AnalyzerName = "keyword")]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("parent_id")]
    [SearchableField(IsFilterable = true)]
    public string ParentId { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.NoLucene)]
    [JsonPropertyName("chunk")]
    public string Chunk { get; set; }

    [VectorSearchField(
        VectorSearchDimensions = 1536, 
        VectorSearchProfileName = Constants.HnswProfile
    )]
    [SearchableField]
    [JsonPropertyName("vector")]
    public float[] Vector { get; set; }
}