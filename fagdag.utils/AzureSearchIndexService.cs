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

        // Dersom indeks eksisterer, slett og opprett ny
        // Vil ikke anbefale denne metoden i prod ...
        try
        {
            var result = await SearchIndexClient.GetIndexAsync(IndexName);
            await SearchIndexClient.DeleteIndexAsync(result.Value.Name);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        // Før vi kan søke i indeksen må søkefrasen gjøres om til en vektor.
        // Det konfigurerer vi her, slik at vi slipper å ha et eget steg for dette i RAG-pipeline.
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
        FieldBuilder builder = new();
        IList<SearchField> fields = builder.Build(typeof(Index));

        // TODO: Lag en instans av søkeindeksen, og inkluder:
        // indeksnavn
        // felter (som du lagde i forrige steg)
        // Similarity skal settes til BM25Similarity
        // vectorSearch-instansen

        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.models.searchindex?view=azure-dotnet
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