using Pinecone;
using System.ClientModel;

namespace Fagdag.Utils;

public interface IPineconeService
{
    Task<Pinecone.Index?> GetIndexAsync(string name);
    Task<uint> UpsertAsync(string @namespace, List<Document> documents);
}

public class PineconeService : IPineconeService
{
    private const string Region = "east-us-1";
    private const string IndexName = "fagdag";
    private PineconeClient _client { get; }

    public PineconeService(
        ApiKeyCredential apiKeyCredential,
        ClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(apiKeyCredential);
        apiKeyCredential.Deconstruct(out string key);

        _client = new(apiKey: key, options);
    }

    public async Task<Pinecone.Index?> GetIndexAsync(string name)
    {
        var indexList = await _client.ListIndexesAsync();
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
    public async Task<uint> UpsertAsync(string @namespace, List<Document> documents)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(@namespace);
        ArgumentNullException.ThrowIfNull(documents);

        var index = _client.Index(IndexName);
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
            Namespace = @namespace
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

        return await _client.CreateIndexAsync(createIndexRequest);
    }

}
