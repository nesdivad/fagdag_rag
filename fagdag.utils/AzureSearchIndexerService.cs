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
    private string Username { get; }
    private SearchIndexerClient SearchIndexerClient { get; }
    private SearchIndexerDataSourceConnection DataSourceConnection { get; }
    private ApiKeyCredential AzureOpenaiApiKey { get; }
    private ApiKeyCredential CognitiveServicesApiKey { get; }
    private Uri? AzureOpenaiEndpoint { get; }

    public AzureSearchIndexerService(IConfiguration configuration)
    {
        var username = configuration[Constants.Username];
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

        Username = username;
        IndexName = $"index{Username}";
        IndexerName = $"indexer{Username}";
        SkillsetName = $"skillset{Username}";

        SearchIndexerClient = new(new Uri(azureSearchEndpoint), new AzureKeyCredential(azureSearchApiKey));
        AzureOpenaiApiKey = new(azureOpenaiApiKey);
        CognitiveServicesApiKey = new(azureCognitiveServicesApiKey);

        SearchIndexerDataSourceConnection dataSourceConnection = new(
            "fagdag",
            SearchIndexerDataSourceType.AzureBlob,
            azureStorageConnectionString,
            new SearchIndexerDataContainer("markdown")
        );

        try
        {
            AzureOpenaiEndpoint = new Uri(azureOpenaiEmbeddingEndpoint);
            SearchIndexerClient.CreateOrUpdateDataSourceConnection(dataSourceConnection);
        }
        catch (UriFormatException u)
        {
            Console.WriteLine($"En feil oppsto ved registrering av endepunkt for Azure AI Services.\nVennligst sjekk formatet i appsettings.json\n{u.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"En feil oppsto ved oppretting av datakilde.\nException: {e.Message}");
        }

        DataSourceConnection = dataSourceConnection;
    }

    /**
     * <summary>Opprett skills, lag et nytt skillset og deploy det til ressursen i Azure
     */
    public async Task<SearchIndexerSkillset> CreateSkillsetAsync()
    {
        // TODO: Dekonstruer API-nøkkel for Azure OpenAI og Cognitive Services
        AzureOpenaiApiKey.Deconstruct(out string azureOpenaiApiKey);
        CognitiveServicesApiKey.Deconstruct(out string cognitiveServicesApiKey);

        // TODO: Opprett en instans av hver skill du ønsker å bruke
        var piiSkill = GetPiiDetectionSkill();

        var splitSkill = GetSplitSkill(
            maximumPageLength: 2000,
            pageOverlapLength: 500
        );
        
        var embeddingSkill = GetEmbeddingSkill(
            apiKey: azureOpenaiApiKey,
            endpoint: AzureOpenaiEndpoint!
        );

        // TODO: Lag en liste med alle skills
        List<SearchIndexerSkill> skills = [
            // piiSkill,
            splitSkill,
            embeddingSkill
        ];

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
        SearchIndexerSkillset? indexerSkillset = await CreateOrUpdateSearchIndexerSkillset(
            skills: skills,
            indexProjection: indexProjection,
            aiServicesApiKey: cognitiveServicesApiKey
        );

        if (indexerSkillset is null)
            throw new NullReferenceException(nameof(indexerSkillset));

        // TODO: Fjern denne når implementasjonen er klar
        // throw new NotImplementedException();

        return indexerSkillset;
    }

    public async Task<SearchIndexer> CreateOrUpdateIndexerAsync()
    {
        try
        {
            await SearchIndexerClient.GetIndexerAsync(indexerName: IndexerName);
            await SearchIndexerClient.DeleteIndexerAsync(indexerName: IndexerName);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        // https://learn.microsoft.com/en-us/azure/search/cognitive-search-tutorial-blob-dotnet#step-4-create-and-run-an-indexer
        IndexingParameters indexingParameters = new()
        {
            MaxFailedItems = -1,
            MaxFailedItemsPerBatch = -1,
            IndexingParametersConfiguration = []
        };

        indexingParameters.IndexingParametersConfiguration.Add(
            key: "dataToExtract",
            value: "contentAndMetadata"
        );

        SearchIndexer indexer = new(
            name: IndexerName,
            dataSourceName: DataSourceConnection.Name,
            targetIndexName: IndexName)
        {
            Parameters = indexingParameters,
            SkillsetName = SkillsetName 
        };

        indexer = await SearchIndexerClient.CreateOrUpdateIndexerAsync(indexer);

        return indexer;
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
        string aiServicesApiKey)
    {
        var skillset = await GetSkillsetAsync(SkillsetName);
        if (skillset is not null)
            await DeleteSkillsetAsync(SkillsetName);

        SearchIndexerSkillset searchIndexerSkillset = new(DefaultSkillset, skills)
        {
            Name = SkillsetName,
            Description = "Samling av skills som brukes i prosesseringen",
            CognitiveServicesAccount = new CognitiveServicesAccountKey(aiServicesApiKey),
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
     * <summary>Finn personlig identifiserbar informasjon, og maskér innholdet.</summary>
     */
    private static PiiDetectionSkill GetPiiDetectionSkill(double minimumPrecision = 0.5)
    {
        List<InputFieldMappingEntry> inputMappings = [
            new("text") { Source = "/document/content" }
        ];
        List<OutputFieldMappingEntry> outputMappings = [
            new("maskedText") { TargetName = "/document/maskedText"}
        ];

        var pii = new PiiDetectionSkill(inputMappings, outputMappings)
        {
            Name = "PII detection",
            Description = "Detect personally identifiable information in the text, and mask it.",
            DefaultLanguageCode = "nb",

            // Erstatter PII med '*'
            Mask = "*",
            MaskingMode = PiiDetectionSkillMaskingMode.Replace,

            // Minimum presisjon på en skala fra 0 til 1.
            // Måles opp mot konfidensnivå i resultat fra AI-modellen.
            MinPrecision = minimumPrecision
        };

        return pii;
    }

    /**
     * <summary>Splitt dokumenter opp i mindre biter</summary>
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
     */
    private static AzureOpenAIEmbeddingSkill GetEmbeddingSkill(
        string apiKey,
        Uri endpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        ArgumentNullException.ThrowIfNull(endpoint);

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
            ApiKey = apiKey,
            DeploymentName = Constants.TextEmbedding3Large,
            Dimensions = 1536,
            ModelName = Constants.TextEmbedding3Large,
            ResourceUri = endpoint,
            Context ="/document/pages/*"
        };
    }
}