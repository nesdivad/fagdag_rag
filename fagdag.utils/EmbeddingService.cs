using Azure.Identity;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAI.Embeddings;

namespace Fagdag.Utils;

public interface IAzureOpenAIService
{
    Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string input, EmbeddingGenerationOptions? embeddingGenerationOptions = null);
}

public class AzureOpenAIService : IAzureOpenAIService
{
    private ChatClient _chatClient { get; }
    private EmbeddingClient? _embeddingClient { get; }

    public AzureOpenAIService(Uri endpoint,
        string deploymentName,
        string? embeddingDeploymentName = null,
        string? apiKey = null,
        AzureOpenAIClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(deploymentName);

        var client = string.IsNullOrEmpty(apiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), options)
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey), options);

        if (!string.IsNullOrEmpty(embeddingDeploymentName))
        {
            _embeddingClient = client.GetEmbeddingClient(embeddingDeploymentName);
        }

        _chatClient = client.GetChatClient(deploymentName);
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string input, EmbeddingGenerationOptions? embeddingGenerationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(_embeddingClient);
        embeddingGenerationOptions ??= new EmbeddingGenerationOptions();

        var result = await _embeddingClient.GenerateEmbeddingAsync(input, embeddingGenerationOptions);
        return result.Value.ToFloats();
    }
}
