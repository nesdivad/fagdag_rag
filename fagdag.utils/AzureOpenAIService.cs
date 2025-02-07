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
    private ChatClient ChatClient { get; }
    private EmbeddingClient? EmbeddingClient { get; }

    public AzureOpenAIService(Uri endpoint,
        string apiKey,
        string deploymentName,
        string embeddingDeploymentName,
        AzureOpenAIClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(deploymentName);

        var client = string.IsNullOrEmpty(apiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), options)
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey), options);

        if (!string.IsNullOrEmpty(embeddingDeploymentName))
        {
            EmbeddingClient = client.GetEmbeddingClient(embeddingDeploymentName);
        }

        ChatClient = client.GetChatClient(deploymentName);
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string input, EmbeddingGenerationOptions? embeddingGenerationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(EmbeddingClient);
        embeddingGenerationOptions ??= new()
        {
            Dimensions = 1536
        };

        var result = await EmbeddingClient.GenerateEmbeddingAsync(input, embeddingGenerationOptions);
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

        return await ChatClient.CompleteChatAsync(chatMessages, options);
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

        await foreach (var update in ChatClient.CompleteChatStreamingAsync(chatMessages, options))
        {
            yield return update;
        }
    }
}
