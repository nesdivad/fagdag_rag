using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Fagdag.Embeddings;

public static class Jobbnorge
{
    public static async Task<List<Job>> GetJobsAsync()
    {
        HttpClient httpClient = new();
        var response = await httpClient.GetAsync("https://publicapi.jobbnorge.no/v2.0/Jobs?abroad=false");
        response.EnsureSuccessStatusCode();
        
        var jobs = await response.Content.ReadFromJsonAsync<List<Job>>() ?? [];
        return jobs;
    }
}

public record Job
{
    [JsonPropertyName("id")]
    public required string Id = string.Empty;

    [JsonPropertyName("jobScope")]
    public string JobScope = string.Empty;

    [JsonPropertyName("jobDuration")]
    public string JobDuration = string.Empty;

    [JsonPropertyName("deadline")]
    public string Deadline = string.Empty;

    [JsonPropertyName("employer")]
    public string Employer = string.Empty;

    [JsonPropertyName("link")]
    public string Link = string.Empty;

    [JsonPropertyName("department")]
    public string Department = string.Empty;

    [JsonPropertyName("title")]
    public required string Title = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary = string.Empty;

    [JsonPropertyName("publicationDate")]
    public string PublicationDate = string.Empty;
}