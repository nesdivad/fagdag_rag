using Pinecone;
using System.ClientModel;

namespace Fagdag.Utils;

public interface IPineconeService
{
    Task<Pinecone.Index?> GetIndexAsync(string name);
    IndexClient GetIndexClient(Pinecone.Index index, 
        ClientOptions? clientOptions = null);
}

public class PineconeService : IPineconeService
{
    private const string Region = "east-us-1";
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

    public IndexClient GetIndexClient(
        Pinecone.Index index, 
        ClientOptions? clientOptions = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        return _client.Index(index.Name, index.Host, clientOptions);
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
            }
        };

        return await _client.CreateIndexAsync(createIndexRequest);
    }

}
