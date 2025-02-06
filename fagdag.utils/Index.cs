using System.Text.Json.Serialization;

using Azure.Search.Documents.Indexes;

namespace Fagdag.Utils;

public class Index
{
    [SearchableField(IsSortable = true, IsKey = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [SearchableField]
    [JsonPropertyName("content")]
    public string Content { get; set; }

    [SearchableField]
    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; }
}