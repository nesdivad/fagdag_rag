using System.ClientModel;

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
    private ApiKeyCredential ApiKeyCredential { get; }
    private Uri AzureOpenaiEndpoint { get; }
    public AzureSearchIndexService(IConfiguration configuration)
    {
        var username = configuration[Constants.Username];
        var azureSearchApiKey = configuration[Constants.AzureSearchApiKey];
        var azureSearchEndpoint = configuration[Constants.AzureSearchEndpoint];
        var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];
        var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEmbeddingEndpoint];

        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
        
        IndexName = $"index{username}";
        ApiKeyCredential = new(azureOpenaiApiKey);
        AzureOpenaiEndpoint = new Uri(azureOpenaiEndpoint);
        
        SearchIndexClient = new(
            endpoint: new Uri(azureSearchEndpoint), 
            credential: new AzureKeyCredential(azureSearchApiKey)
        );
    }

    public async Task<SearchIndex> CreateOrUpdateSearchIndexAsync()
    {
        ApiKeyCredential.Deconstruct(out string apiKey);

        // For å unngå oppretting av nye indekser gjøres det først en get
        try
        {
            var result = await SearchIndexClient.GetIndexAsync(IndexName);
            await SearchIndexClient.DeleteIndexAsync(result.Value.Name);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        // TODO: Definér skjemaet for indeksen
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.fieldbuilder?view=azure-dotnet

        
        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(
            new HnswAlgorithmConfiguration(Constants.HnswProfile)
            {
                Parameters = new()
                {
                    Metric = VectorSearchAlgorithmMetric.Cosine,
                    M = 4,
                    EfConstruction = 400,
                    EfSearch = 500
                }
            }
        );

        vectorSearch.Profiles.Add(
            new VectorSearchProfile(Constants.HnswProfile, Constants.HnswProfile)
            { VectorizerName = Constants.OpenAIVectorizer }
        );

        vectorSearch.Vectorizers.Add(
            new AzureOpenAIVectorizer(Constants.OpenAIVectorizer)
            {
                Parameters = new AzureOpenAIVectorizerParameters()
                {
                    ApiKey = apiKey,
                    DeploymentName = Constants.TextEmbedding3Large,
                    ModelName = Constants.TextEmbedding3Large,
                    ResourceUri = AzureOpenaiEndpoint
                }
            }
        );

        // TODO: Lag en instans av søkeindeksen
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.models.searchindex?view=azure-dotnet

        FieldBuilder builder = new();
        IList<SearchField> fields = builder.Build(typeof(Index));

        SearchIndex searchIndex = new(IndexName, fields)
        {
            Similarity = new BM25Similarity(),
            VectorSearch = vectorSearch
        };

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