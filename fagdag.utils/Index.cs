using System.Text.Json.Serialization;

using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Fagdag.Utils;

public class Index
{
    [SearchableField(IsSortable = true, IsKey = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.NoLucene)]
    [JsonPropertyName("content")]
    public string Content { get; set; }

    [SearchableField]
    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}