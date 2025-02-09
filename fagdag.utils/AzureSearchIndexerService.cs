using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;

using System.ClientModel;

namespace Fagdag.Utils;

public interface IAzureSearchIndexerService
{
    Task GetIndexerStatus(SearchIndexer indexer);
    Task<SearchIndex> CreateOrUpdateSearchIndexAsync();
    Task<SearchIndexer> CreateOrUpdateIndexerAsync();
    Task<SearchIndexerSkillset> CreateSkillsetAsync();
}

public class AzureSearchIndexerService : IAzureSearchIndexerService
{
    private const string TextEmbeddingLarge = "text-embedding-3-large";

    // Bruk denne hvis ting går sideveis...
    private const string DefaultSkillset = "default";
    private string IndexName { get; init; }
    private string IndexerName { get; init; }
    private string Username { get; init; }
    private SearchIndexClient SearchIndexClient { get; init; }
    private SearchIndexerClient SearchIndexerClient { get; init; }
    private SearchIndexerDataSourceConnection DataSourceConnection { get; init; }
    private ApiKeyCredential AIServicesApiKey { get; init; }
    private Uri? AIServicesEndpoint { get; init; }

    public AzureSearchIndexerService(IConfiguration configuration)
    {
        var username = configuration[Constants.Username];
        var azureSearchApiKey = configuration[Constants.AzureSearchApiKey];
        var azureSearchEndpoint = configuration[Constants.AzureSearchEndpoint];
        var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];
        var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
        var azureStorageConnectionString = configuration[Constants.AzureStorageConnectionString];

        ArgumentException.ThrowIfNullOrEmpty(azureSearchApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureSearchEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(azureStorageConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(username);

        Username = username;
        IndexName = $"index_{Username}";
        IndexerName = $"indexer_{Username}";

        SearchIndexClient = new(new Uri(azureSearchEndpoint), new AzureKeyCredential(azureSearchApiKey));
        SearchIndexerClient = new(new Uri(azureSearchEndpoint), new AzureKeyCredential(azureSearchApiKey));
        AIServicesApiKey = new(azureOpenaiApiKey);

        SearchIndexerDataSourceConnection dataSourceConnection = new(
            "fagdag",
            SearchIndexerDataSourceType.AzureBlob,
            azureStorageConnectionString,
            new SearchIndexerDataContainer("markdown")
        );
        try
        {
            AIServicesEndpoint = new Uri(azureOpenaiEndpoint);
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
        // TODO: Dekonstruer API-nøkkel for AI Services
        AIServicesApiKey.Deconstruct(out string apiKey);

        // TODO: Opprett en instans av hver skill du ønsker å bruke
        var piiSkill = GetPiiDetectionSkill();
        var splitSkill = GetSplitSkill(
            maximumPageLength: 1000,
            pageOverlapLength: 250
        );
        var embeddingSkill = GetEmbeddingSkill(
            apiKey: apiKey,
            endpoint: AIServicesEndpoint ?? throw new NullReferenceException(nameof(AIServicesEndpoint))
        );

        // TODO: Lag en liste med alle skills
        List<SearchIndexerSkill> skills = [
            piiSkill,
            splitSkill,
            embeddingSkill
        ];

        // TODO: Deploy skillsettet til ressursen i Azure
        SearchIndexerSkillset indexerSkillset = await CreateOrUpdateSearchIndexerSkillset(
            indexerClient: SearchIndexerClient,
            skills: skills,
            aiServicesApiKey: apiKey
        );

        // TODO: Fjern denne når implementasjonen er klar
        // throw new NotImplementedException();

        return indexerSkillset;
    }

    public async Task<SearchIndex> CreateOrUpdateSearchIndexAsync()
    {
        FieldBuilder builder = new();
        SearchIndex searchIndex = new(IndexName)
        {
            Fields = builder.Build(typeof(Index))
        };

        try
        {
            await SearchIndexClient.GetIndexAsync(searchIndex.Name);
            await SearchIndexClient.DeleteIndexAsync(searchIndex.Name);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        try
        {
            await SearchIndexClient.CreateOrUpdateIndexAsync(
                searchIndex,
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

    public async Task<SearchIndexer> CreateOrUpdateIndexerAsync()
    {
        // https://learn.microsoft.com/en-us/azure/search/cognitive-search-tutorial-blob-dotnet#step-4-create-and-run-an-indexer
        IndexingParameters indexingParameters = new()
        {
            MaxFailedItems = -1,
            MaxFailedItemsPerBatch = -1
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
            Parameters = indexingParameters
        };

        FieldMappingFunction fieldMappingFunction = new("base64Encode");

        indexer.FieldMappings.Add(new("content")
        {
            TargetFieldName = "content"
        });

        try
        {
            await SearchIndexerClient.GetIndexerAsync(indexer.Name);
            await SearchIndexerClient.DeleteIndexerAsync(indexer.Name);
        }
        catch (RequestFailedException ex) when (ex.Status is 404) { }

        try
        {
            await SearchIndexerClient.CreateOrUpdateIndexerAsync(indexer);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"En feil oppsto under oppretting av indekserer.\n{ex.Message}");
        }

        return indexer;
    }

    public async Task GetIndexerStatus(SearchIndexer indexer)
    {
        try
        {
            var executionInfo = await SearchIndexerClient.GetIndexerStatusAsync(indexer.Name);
            switch (executionInfo.Value.Status)
            {
                case IndexerStatus.Running: Console.WriteLine("Indexer is running ..."); break;
                case IndexerStatus.Unknown: Console.WriteLine("Indexer status unknown."); break;
                case IndexerStatus.Error: Console.WriteLine("Indexer returned an error =("); break;
                default: break;
            }
            ;
        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"En feil oppsto under henting av status for indekserer.\n{e.Message}");
        }
    }

    /**
     * <summary>Opprett eller oppdater et skillset</summary>
     */
    private static async Task<SearchIndexerSkillset> CreateOrUpdateSearchIndexerSkillset(
        SearchIndexerClient indexerClient,
        IList<SearchIndexerSkill> skills,
        string aiServicesApiKey)
    {
        SearchIndexerSkillset searchIndexerSkillset = new(DefaultSkillset, skills)
        {
            Name = "skillset",
            Description = "Samling av skills som brukes i prosesseringen",
            CognitiveServicesAccount = new CognitiveServicesAccountKey(aiServicesApiKey)
        };

        try
        {
            await indexerClient.CreateSkillsetAsync(searchIndexerSkillset);
        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"En feil oppsto under oppretting av skillset\n{e.Message}");
        }

        return searchIndexerSkillset;
    }

    /**
     * <summary>Finn personlig identifiserbar informasjon, og maskér innholdet.</summary>
     */
    private static PiiDetectionSkill GetPiiDetectionSkill(
        double minimumPrecision = 0.5)
    {
        List<InputFieldMappingEntry> inputMappings = [
            new("text") { Source = "/document/content" }
        ];
        List<OutputFieldMappingEntry> outputMappings = [
            new("maskedText")
        ];

        return new PiiDetectionSkill(inputMappings, outputMappings)
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
            new("text") { Source = "/document/maskedText" }
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
            Context = "/document",
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
            DeploymentName = TextEmbeddingLarge,
            Dimensions = 1536,
            ModelName = TextEmbeddingLarge,
            ResourceUri = endpoint
        };
    }
}