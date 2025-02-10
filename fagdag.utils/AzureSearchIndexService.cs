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
    private SearchIndexClient SearchIndexClient { get; }
    public AzureSearchIndexService(IConfiguration configuration)
    {
        var username = configuration[Constants.Username];
        var azureSearchApiKey = configuration[Constants.AzureSearchApiKey];
        var azureSearchEndpoint = configuration[Constants.AzureSearchEndpoint];

        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchEndpoint);
        
        IndexName = $"index{username}";
        
        SearchIndexClient = new(
            endpoint: new Uri(azureSearchEndpoint), 
            credential: new AzureKeyCredential(azureSearchApiKey)
        );
    }

    public async Task<SearchIndex> CreateOrUpdateSearchIndexAsync()
    {
        // For å unngå oppretting av nye indekser gjøres det først en get
        try
        {
            var result = await SearchIndexClient.GetIndexAsync(IndexName);
            await SearchIndexClient.DeleteIndexAsync(result.Value.Name);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        // TODO: Definér skjemaet for indeksen
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.fieldbuilder?view=azure-dotnet

        FieldBuilder builder = new();
        IList<SearchField> fields = builder.Build(typeof(Index));
        
        // TODO: Lag en instans av søkeindeksen
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.models.searchindex?view=azure-dotnet
        SearchIndex searchIndex = new(IndexName, fields);
        
        // TODO: Opprett søkeindeksen
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.searchindexclient.createorupdateindexasync?view=azure-dotnet
        await SearchIndexClient.CreateOrUpdateIndexAsync(
            index: searchIndex,
            allowIndexDowntime: true
        );
        
        return searchIndex;

        // TODO: Fjern når du er ferdig
        // throw new NotImplementedException();
    }
}