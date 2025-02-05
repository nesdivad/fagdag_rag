using Fagdag.Utils;

using Microsoft.Extensions.Configuration;

using OneOf;

using Pinecone;

using Spectre.Console;

namespace Fagdag.Embeddings;

public class State(IConfiguration configuration)
{
    public List<Job> Jobs { get; set; } = [];
    public List<Document> Documents { get; set; } = [];

    private IConfiguration Configuration { get; } = configuration;
    private AzureOpenAIService? AzureOpenAIService { get; set; }
    private PineconeService? PineconeService { get; set; }

    private void InitServices()
    {
        var endpoint = Configuration["AZURE_OPENAI_ENDPOINT"];
        var apiKey = Configuration["AZURE_OPENAI_API_KEY"];
        var pineconeApiKey = Configuration["PINECONE_API_KEY"];
        var @namespace = Configuration["Username"];

        if (string.IsNullOrEmpty(endpoint))
            throw new NullReferenceException(nameof(endpoint));
        if (string.IsNullOrEmpty(apiKey))
            throw new NullReferenceException(nameof(apiKey));
        if (string.IsNullOrEmpty(pineconeApiKey))
            throw new NullReferenceException(nameof(pineconeApiKey));
        if (string.IsNullOrEmpty(@namespace))
            throw new NullReferenceException(nameof(@namespace));

        AzureOpenAIService = new AzureOpenAIService(
            new Uri(endpoint),
            "gpt-4o",
            "text-embedding-3-large",
            apiKey
        );

        PineconeService = new PineconeService(pineconeApiKey, @namespace);
    }

    public async Task<uint> DoAllTheStuff()
    {
        /*
         * TODO:
         * 
         * Assemble all the bits and pieces in this function. This includes:
         * 1. Processing the text
         * 2. Creating the embeddings
         * 3. Storing the embeddings together with the document in the database
         */

        Documents = ProcessText();
        await CreateEmbeddings();
        var count = await StoreInDatabase();
        return count;
    }

    public List<Document> ProcessText()
    {
        #region [ Don't look here ]

        if (Jobs is { Count: 0 })
            return []; // throw something

        if (AzureOpenAIService is null || PineconeService is null)
            InitServices();

        #endregion

        return [.. Jobs.Select(x =>
        {
            var content = $"{x.Title}\n{x.Summary}";
            var metadata = new Metadata
            {
                ["link"] = x.Link,
                ["jobDuration"] = x.JobDuration,
                ["jobScope"] = x.JobScope,
                ["employer"] = x.Employer,
                ["department"] = x.Department,
                ["title"] = x.Title,
                ["content"] = content
            };

            return new Document(x.Id, content, metadata: metadata);
        })];
    }

    public async Task CreateEmbeddings()
    {
        #region [ Don't look ... ]

        if (AzureOpenAIService is null)
            throw new NullReferenceException(nameof(AzureOpenAIService));
        if (Documents is { Count: 0 } || Documents.Any(x => x.Values is not null))
            return; // throw something

        #endregion

        for (var i = 0; i < Documents.Count; i++)
        {
            var doc = Documents[i];
            var values = await AzureOpenAIService.GetEmbeddingsAsync(doc.Content);

            if (i is 0)
                AnsiConsole.Markup($"Values for element 0:\n[yellow]{values}[/]");

            doc.Values = values.ToArray();
        }
    }

    public async Task<uint> StoreInDatabase()
    {
        #region [ No interesting stuff going on here ]

        if (PineconeService is null)
            throw new NullReferenceException(nameof(PineconeService));
        if (Documents.Any(x => x.Values is null))
            return 0; // throw something

        #endregion

        uint count = await PineconeService.UpsertAsync(Documents);
        return count;
    }
}