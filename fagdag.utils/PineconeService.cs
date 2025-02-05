using Pinecone;
using System.ClientModel;

namespace Fagdag.Utils;

public interface IPineconeService
{
    Task<Pinecone.Index?> GetIndexAsync(string name);
    Task<uint> UpsertAsync(List<Document> documents);
}

public class PineconeService : IPineconeService
{
    private const string Region = "east-us-1";
    private const string IndexName = "fagdag";
    private PineconeClient Client { get; }
    private string Namespace { get; init; }

    public PineconeService(
        string apiKey,
        string @namespace,
        ClientOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        var apiKeyCredential = new ApiKeyCredential(apiKey);
        apiKeyCredential.Deconstruct(out string key);

        options ??= new();
        
        Client = new(apiKey: key, options);
        Namespace = @namespace;
    }

    public async Task<Pinecone.Index?> GetIndexAsync(string name)
    {
        var indexList = await Client.ListIndexesAsync();
        if (indexList.Indexes is null)
            return await CreateIndexAsync(name);

        var index = indexList.Indexes.FirstOrDefault(
            x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return index ?? await CreateIndexAsync(name);
    }

    /**
     * <summary>
     *  Upserts documents to the index.
     *  Returns: Number of upserts
     * </summary>
     */
    public async Task<uint> UpsertAsync(List<Document> documents)
    {
        ArgumentException.ThrowIfNullOrEmpty(Namespace);
        ArgumentNullException.ThrowIfNull(documents);

        var index = Client.Index(IndexName);
        var vectors = new List<Vector>();
        var chunks = documents.Chunk(200);

        foreach (var chunk in chunks)
        {
            foreach (var doc in chunk)
            {
                vectors.Add(doc.ToVector());
            }
        }

        var response = await index.UpsertAsync(new UpsertRequest
        {
            Vectors = vectors,
            Namespace = Namespace
        });

        return response?.UpsertedCount ?? 0;
    }

    private async Task<Pinecone.Index> CreateIndexAsync(
        string name,
        CreateIndexRequest? createIndexRequest = null)
    {
        createIndexRequest ??= new()
        {
            Name = name,
            Dimension = 1536,
            Spec = new ServerlessIndexSpec
            {
                Serverless = new ServerlessSpec
                {
                    Cloud = ServerlessSpecCloud.Aws,
                    Region = Region
                }
            },

            DeletionProtection = DeletionProtection.Enabled
        };

        return await Client.CreateIndexAsync(createIndexRequest);
    }

}
