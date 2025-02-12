using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;

using System.ClientModel;

namespace Fagdag.Utils;

public interface IAzureSearchIndexerService
{
    Task<IndexerExecutionResult> GetIndexerStatus(SearchIndexer indexer);
    Task<SearchIndexer> CreateOrUpdateIndexerAsync();
    Task<SearchIndexerSkillset> CreateSkillsetAsync();
}

public class AzureSearchIndexerService : IAzureSearchIndexerService
{
    // Bruk denne hvis ting går sideveis...
    private const string DefaultSkillset = "default";
    private string SkillsetName { get; }
    private string IndexName { get; }
    private string IndexerName { get; }
    private SearchIndexerClient SearchIndexerClient { get; }
    private SearchIndexerDataSourceConnection DataSourceConnection { get; }
    private ApiKeyCredential AzureOpenaiApiKey { get; }
    private ApiKeyCredential CognitiveServicesApiKey { get; }
    private Uri AzureOpenaiEndpoint { get; }

    public AzureSearchIndexerService(IConfiguration configuration)
    {
        var username = TangOgTare.GetOrCreateUsername();
        var azureSearchApiKey = configuration[Constants.AzureSearchApiKey];
        var azureSearchEndpoint = configuration[Constants.AzureSearchEndpoint];
        var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];
        var azureStorageConnectionString = configuration[Constants.AzureStorageConnectionString];
        var azureOpenaiEmbeddingEndpoint = configuration[Constants.AzureOpenAIEmbeddingEndpoint];
        var azureCognitiveServicesApiKey = configuration[Constants.AzureCognitiveServicesApiKey];

        ArgumentException.ThrowIfNullOrEmpty(azureSearchApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureCognitiveServicesApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureStorageConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEmbeddingEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(username);

        IndexName = $"index{username}";
        IndexerName = $"indexer{username}";
        SkillsetName = $"skillset{username}";

        SearchIndexerClient = new(new Uri(azureSearchEndpoint), new AzureKeyCredential(azureSearchApiKey));
        AzureOpenaiApiKey = new(azureOpenaiApiKey);
        CognitiveServicesApiKey = new(azureCognitiveServicesApiKey);

        SearchIndexerDataSourceConnection dataSourceConnection = new(
            "fagdag",
            SearchIndexerDataSourceType.AzureBlob,
            azureStorageConnectionString,
            new SearchIndexerDataContainer("markdown")
        );

        AzureOpenaiEndpoint = new Uri(azureOpenaiEmbeddingEndpoint);
        SearchIndexerClient.CreateOrUpdateDataSourceConnection(dataSourceConnection);
        
        DataSourceConnection = dataSourceConnection;
    }

    /**
     * <summary>Opprett skills, lag et nytt skillset og deploy det til Azure AI Search</summary>
     * <returns>Liste med skills</returns>
     */
    public async Task<SearchIndexerSkillset> CreateSkillsetAsync()
    {
        // Dekonstruer API-nøkkel for Azure OpenAI og Cognitive Services
        AzureOpenaiApiKey.Deconstruct(out string azureOpenaiApiKey);
        CognitiveServicesApiKey.Deconstruct(out string cognitiveServicesApiKey);

        // TODO: Opprett en instans av hver skill (split skill og embedding skill)
        

        // TODO: Lag en liste med alle skills

        
        // Prosjekter felter fra skillset over til dokumentet i indeksen
        IList<SearchIndexerIndexProjectionSelector> selectors = [
            new SearchIndexerIndexProjectionSelector(
                targetIndexName: IndexName, 
                parentKeyFieldName: "parent_id", 
                sourceContext: "/document/pages/*",
                mappings: [
                    new("vector") { Source = "/document/pages/*/vector" },
                    new("chunk") { Source = "/document/pages/*" }
                ]
            )
        ];

        SearchIndexerIndexProjection indexProjection = new(selectors: selectors)
        {
            Parameters = new SearchIndexerIndexProjectionsParameters() 
            { 
                // Siden vi splitter opp et dokument i mange, får vi en "parent". Vi ønsker ikke å indeksere denne.
                ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments 
            }
        };

        // TODO: Deploy skillsettet til ressursen i Azure

        
        // TODO: Fjern denne når implementasjonen er klar
        // return indexerSkillset;
        throw new NotImplementedException();
    }

    /**
     *<summary>Opprett indekserer i Azure AI Search</summary>
     *<returns>Instans av indekserer</returns>
     */
    public async Task<SearchIndexer> CreateOrUpdateIndexerAsync()
    {
        // Fjerner indekserer hvis den allerede eksisterer
        try
        {
            await SearchIndexerClient.GetIndexerAsync(indexerName: IndexerName);
            await SearchIndexerClient.DeleteIndexerAsync(indexerName: IndexerName);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) {}

        // TODO: Lag en instans av 'IndexingParameters', og sett følgende felter:
        // - MaxFailedItems = -1 (indekserer kjører uansett hvor mange feil du får)
        // - MaxFailedItemsPerBatch = -1 (indekserer kjører uansett hvor mange feil du får)
        // - IndexingParametersConfiguration = []
        // Eksempel: https://learn.microsoft.com/en-us/azure/search/cognitive-search-tutorial-blob-dotnet#step-4-create-and-run-an-indexer
        // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.models.indexingparameters?view=azure-dotnet


        // TODO: Legg til konfigurasjon for IndexingParametersConfiguration:
        // key: "dataToExtract", value: "contentAndMetadata"
        
        
        // TODO: Lag en ny instans av 'SearchIndexer', og inkluder:
        // - indeksnavn
        // - navn på datakilde (DataSourceConnection)
        // - navn på søkeindeks
        // - IndexingParameters som du lagde i forrige steg
        // - Navn på skillset


        // TODO: Opprett indekserer i Azure AI Search


        // TODO: Returner indekserer
        //return indexer;
        throw new NotImplementedException();
    }

    public async Task<IndexerExecutionResult> GetIndexerStatus(SearchIndexer indexer)
    {               
        var indexerStatus = await SearchIndexerClient.GetIndexerStatusAsync(indexer.Name);
        return indexerStatus.Value.LastResult;   
    }

    private async Task<SearchIndexerSkillset?> GetSkillsetAsync(string skillsetName)
    {
        try
        {
            return await SearchIndexerClient.GetSkillsetAsync(skillsetName);
        }
        catch (RequestFailedException e) when (e.Status is 404)
        {
            return null;
        }
    }

    private async Task DeleteSkillsetAsync(string skillsetName)
    {
        try
        {
            await SearchIndexerClient.DeleteSkillsetAsync(skillsetName);
        }
        catch (RequestFailedException e) when (e.Status is 404) {}
    }

    /**
     * <summary>Opprett eller oppdater et skillset</summary>
     */
    private async Task<SearchIndexerSkillset?> CreateOrUpdateSearchIndexerSkillset(
        IList<SearchIndexerSkill> skills,
        SearchIndexerIndexProjection indexProjection,
        string cognitiveServicesApiKey)
    {
        var skillset = await GetSkillsetAsync(SkillsetName);
        if (skillset is not null)
            await DeleteSkillsetAsync(SkillsetName);

        SearchIndexerSkillset searchIndexerSkillset = new(DefaultSkillset, skills)
        {
            Name = SkillsetName,
            Description = "Samling av skills som brukes i prosesseringen",
            CognitiveServicesAccount = new CognitiveServicesAccountKey(cognitiveServicesApiKey),
            IndexProjection = indexProjection
        };

        try
        {
            await SearchIndexerClient.CreateSkillsetAsync(skillset: searchIndexerSkillset);
        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"En feil oppsto under oppretting av skillset\n{e.Message}");
            return null;
        }

        return searchIndexerSkillset;
    }

    /**
     * <summary>Splitt dokumenter opp i mindre biter</summary>
     *<returns>Split skill</returns>
     */
    private static SplitSkill GetSplitSkill(
        int maximumPageLength = 2000,
        int pageOverlapLength = 500)
    {
        List<InputFieldMappingEntry> inputMappings = [
            // legg merke til at source er output fra forrige skill (pii detection)
            new("text") { Source = "/document/content" }
        ];

        List<OutputFieldMappingEntry> outputMappings = [
            new("textItems") { TargetName = "pages" }
        ];

        SplitSkill splitSkill = new(inputMappings, outputMappings)
        {
            Name = "split text",
            Description = "Splits documents into smaller pieces",
            DefaultLanguageCode = SplitSkillLanguage.Nb,

            // Maks lengde for hvert dokument
            MaximumPageLength = maximumPageLength,

            // Hvor mye av teksten som skal overlappe fra et dokument til det neste.
            // Overlapping gjøres for å bevare konteksten over flere splittede dokumenter.
            PageOverlapLength = pageOverlapLength,

            // Bestemmer om dokumentet skal splittes på sider eller i individuelle setninger.
            TextSplitMode = TextSplitMode.Pages,
            MaximumPagesToTake = 0
        };

        return splitSkill;
    }

    /**
     * <summary>Lager embeddings av teksten i hvert dokument.</summary>
     * <returns>Embedding skill</returns>
     */
    private static AzureOpenAIEmbeddingSkill GetEmbeddingSkill(
        string azureOpenaiApiKey,
        Uri azureOpenaiEndpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);
        ArgumentNullException.ThrowIfNull(azureOpenaiEndpoint);

        List<InputFieldMappingEntry> inputMappings = [
            // legg merke til at source er output fra forrige skill (split skill)
            new("text") { Source = "/document/pages/*" }
        ];
        List<OutputFieldMappingEntry> outputMappings = [
            new("embedding") { TargetName = "vector" }
        ];

        return new AzureOpenAIEmbeddingSkill(inputMappings, outputMappings)
        {
            Name = "Embedding skill",
            Description = "Create embeddings from text documents in order to use semantic search in RAG pipeline",
            ApiKey = azureOpenaiApiKey,
            DeploymentName = Constants.TextEmbedding3Large,
            Dimensions = 1536,
            ModelName = Constants.TextEmbedding3Large,
            ResourceUri = azureOpenaiEndpoint,
            Context ="/document/pages/*"
        };
    }
}