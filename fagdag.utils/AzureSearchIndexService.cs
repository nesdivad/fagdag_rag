using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

using Microsoft.Extensions.Configuration;

namespace Fagdag.Utils;

public interface IAzureSearchIndexService
{
    Task<SearchIndex> CreateOrUpdateSearchIndexAsync();
}

public class AzureSearchIndexService : IAzureSearchIndexService
{
    private string IndexName { get; }
    private string Username { get; }
    private SearchIndexClient SearchIndexClient { get; }
    public AzureSearchIndexService(IConfiguration configuration)
    {
        var username = configuration[Constants.Username];
        var azureSearchApiKey = configuration[Constants.AzureSearchApiKey];
        var azureSearchEndpoint = configuration[Constants.AzureSearchEndpoint];

        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchEndpoint);
        
        Username = username;
        IndexName = $"index_{Username}";
        
        SearchIndexClient = new(
            endpoint: new Uri(azureSearchEndpoint), 
            credential: new AzureKeyCredential(azureSearchApiKey)
        );
    }

    public async Task<SearchIndex> CreateOrUpdateSearchIndexAsync()
    {
        try
        {
            var result = await SearchIndexClient.GetIndexAsync(IndexName);
            return result;
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        FieldBuilder builder = new();
        SearchIndex searchIndex = new(IndexName)
        {
            Fields = builder.Build(typeof(Index))
        };

        try
        {
            await SearchIndexClient.CreateOrUpdateIndexAsync(
                index: searchIndex,
                allowIndexDowntime: true,
                onlyIfUnchanged: true
            );
        }
        catch (RequestFailedException)
        {
            Console.WriteLine("Hopper over oppdatering av indeks...");
        }

        return searchIndex;
    }
}