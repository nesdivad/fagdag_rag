using System.ClientModel;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

using Microsoft.Extensions.Configuration;

namespace Fagdag.Utils;

public interface IAzureSearchIndexService
{
    Task<SearchIndex> CreateOrUpdateSearchIndexAsync();
    IAsyncEnumerable<SearchResult<Index>> SearchAsync(ReadOnlyMemory<float> embedding);
    IAsyncEnumerable<SearchResult<Index>> SearchAsync(string searchText, int size = 10);
}

public class AzureSearchIndexService : IAzureSearchIndexService
{
    private string IndexName { get; }
    private SearchIndexClient SearchIndexClient { get; }
    private ApiKeyCredential ApiKeyCredential { get; }
    private Uri AzureOpenaiEndpoint { get; }
    public AzureSearchIndexService(IConfiguration configuration)
    {
        var username = TangOgTare.GetOrCreateUsername();
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

        // Dersom indeks eksisterer, slett og opprett ny
        // Anbefaler ikke denne metoden i prod ...
        try
        {
            var result = await SearchIndexClient.GetIndexAsync(IndexName);
            await SearchIndexClient.DeleteIndexAsync(result.Value.Name);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        // Konfig for vektorsøk
        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(
            // Nærmeste nabo søketeknikk
            new HnswAlgorithmConfiguration(Constants.HnswProfile)
            {
                Parameters = new()
                {
                    // Denne verdien må brukes når vi benytter OpenAI
                    Metric = VectorSearchAlgorithmMetric.Cosine,

                    // Something something bi-directional links ¯\_(ツ)_/¯
                    M = 4
                }
            }
        );

        vectorSearch.Profiles.Add(
            new VectorSearchProfile(Constants.HnswProfile, Constants.HnswProfile)
            { VectorizerName = Constants.OpenAIVectorizer }
        );

        // Konfig for Azure OpenAI Embeddings for å gjøre søkefraser om til embeddings
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

        // TODO: Definér skjemaet for indeksen ved å bruke 'FieldBuilder'-klassen for å bygge en liste med search fields av typen 'SearchField'.
        // Dette skjemaet brukes når du senere skal søke etter dokumenter som en del av RAG-pipelinen.
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.fieldbuilder?view=azure-dotnet


        // TODO: Lag en instans av søkeindeksen (SearchIndex), og inkluder:
        // - indeksnavn
        // - felter (som du lagde i forrige steg)
        // - Similarity skal settes til BM25Similarity
        // - vectorSearch-instansen
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.models.searchindex?view=azure-dotnet
        

        // TODO: Opprett søkeindeksen i Azure AI Search ved å bruke SearchIndexClient
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.searchindexclient.createorupdateindexasync?view=azure-dotnet


        // TODO: Returnér searchIndex
        //return index;
        throw new NotImplementedException();
    }

    /**
     *<summary>Søk etter dokumenter i søkeindeks</summary>
     *<param name="searchText">Tekststreng du ønsker å søke på</param>
     *<param name="size">Hvor mange dokumenter som skal returneres</param>
     *<returns>async enumerator med dokumentene</returns>
     */
    public async IAsyncEnumerable<SearchResult<Index>> SearchAsync(string searchText, int size = 10)
    {
        var options = new SearchOptions
        {
            SearchFields = { "chunk" },
            Select = { "id", "chunk", "parent_id" },
            Size = size
        };
        var documents = await SearchIndexClient
            .GetSearchClient(indexName: IndexName)
            .SearchAsync<Index>(searchText: searchText, options: options);

        await foreach (var doc in documents.Value.GetResultsAsync())
        {
            yield return doc;
        }
    }

    /**
     *<summary>Søk etter dokumenter i søkeindeks</summary>
     *<param name="embedding">Embeddings som skal brukes i søket</param>
     *<returns>async enumerator med dokumentene</returns>
     */
    public async IAsyncEnumerable<SearchResult<Index>> SearchAsync(ReadOnlyMemory<float> embedding)
    {
        var options = new SearchOptions
        {
            VectorSearch = new()
            {
                Queries =
                {
                    new VectorizedQuery(embedding) { Fields = { "vector" }, KNearestNeighborsCount = 3 }
                }
            }
        };

        var documents = await SearchIndexClient
            .GetSearchClient(indexName: IndexName)
            .SearchAsync<Index>(options: options);

        await foreach (var doc in documents.Value.GetResultsAsync())
        {
            yield return doc;
        }
    }
}