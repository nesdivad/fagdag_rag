using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Fagdag.Utils;

public interface IAzureSearchService
{

}

public class AzureSearchService : IAzureSearchService
{
    private SearchIndexClient SearchIndexClient { get; init; }
    private SearchIndexerClient SearchIndexerClient { get; init; }
    
    public AzureSearchService(
        string apiKey, 
        string endpoint, 
        string aiServicesApiKey,
        string storageConnectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(aiServicesApiKey);
        ArgumentException.ThrowIfNullOrEmpty(storageConnectionString);
        
        SearchIndexClient = new(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
        SearchIndexerClient = new(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));

        SearchIndexerDataSourceConnection dataSourceConnection = new(
            "fagdag", 
            SearchIndexerDataSourceType.AzureBlob, 
            storageConnectionString, 
            new SearchIndexerDataContainer("markdown")
        );
        try
        {
            SearchIndexerClient.CreateOrUpdateDataSourceConnection(dataSourceConnection);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to create or update data source connection\nException: {e.Message}");
        }
    }
}