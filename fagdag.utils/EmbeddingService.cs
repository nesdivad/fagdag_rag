using Azure.Identity;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAI.Embeddings;

namespace Fagdag.Utils;

public interface IAzureOpenAIService
{
    Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string input, EmbeddingGenerationOptions? embeddingGenerationOptions = null);
    Task<ClientResult<ChatCompletion>> GetCompletionsAsync(ChatMessage[] chatMessages, ChatCompletionOptions? options = null);
    IAsyncEnumerable<StreamingChatCompletionUpdate> GetCompletionsStreamingAsync(ChatMessage[] chatMessages, ChatCompletionOptions? options = null);
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
        embeddingGenerationOptions ??= new()
        {
            Dimensions = 1536
        };

        var result = await _embeddingClient.GenerateEmbeddingAsync(input, embeddingGenerationOptions);
        return result.Value.ToFloats();
    }

    public async Task<ClientResult<ChatCompletion>> GetCompletionsAsync(ChatMessage[] chatMessages, ChatCompletionOptions? options = null)
    {
        options ??= new()
        {
            ResponseFormat = ChatResponseFormat.CreateTextFormat(),
            MaxOutputTokenCount = 2048,
            StoredOutputEnabled = false,
            Temperature = 0.4f
        };

        return await _chatClient.CompleteChatAsync(chatMessages, options);
    }

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> GetCompletionsStreamingAsync(ChatMessage[] chatMessages, ChatCompletionOptions? options = null)
    {
        options ??= new()
        {
            ResponseFormat = ChatResponseFormat.CreateTextFormat(),
            MaxOutputTokenCount = 2048,
            StoredOutputEnabled = false,
            Temperature = 0.4f
        };

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(chatMessages, options))
        {
            yield return update;
        }
    }
}
