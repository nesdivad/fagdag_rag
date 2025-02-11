using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;

namespace Fagdag.Utils;

public interface IAzureOpenAIService
{
    Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(
        string input, 
        EmbeddingGenerationOptions? options = null
    );
    Task<ClientResult<ChatCompletion>> GetCompletionsAsync(
        ChatMessage[] chatMessages, 
        ChatCompletionOptions? options = null
    );
    AsyncCollectionResult<StreamingChatCompletionUpdate> GetCompletionsStreamingAsync(
        ChatMessage[] chatMessages, 
        ChatCompletionOptions? options = null
    );
}

public class AzureOpenAIService : IAzureOpenAIService
{
    private ChatClient ChatClient { get; }
    private EmbeddingClient? EmbeddingClient { get; }

    public AzureOpenAIService(IConfiguration configuration)
    {
        var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
        var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];

        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);

        var client = new AzureOpenAIClient(new Uri(azureOpenaiEndpoint), 
            new ApiKeyCredential(azureOpenaiApiKey));

        EmbeddingClient = client.GetEmbeddingClient(Constants.TextEmbedding3Large);
        ChatClient = client.GetChatClient(Constants.Gpt4o);
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(
        string input, 
        EmbeddingGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(EmbeddingClient);
        options ??= new()
        {
            Dimensions = 1536
        };

        var result = await EmbeddingClient.GenerateEmbeddingAsync(input, options);
        return result.Value.ToFloats();
    }

    public async Task<ClientResult<ChatCompletion>> GetCompletionsAsync(
        ChatMessage[] chatMessages, 
        ChatCompletionOptions? options = null)
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

    public AsyncCollectionResult<StreamingChatCompletionUpdate> GetCompletionsStreamingAsync(
        ChatMessage[] chatMessages, 
        ChatCompletionOptions? options = null)
    {
        options ??= new()
        {
            ResponseFormat = ChatResponseFormat.CreateTextFormat(),
            MaxOutputTokenCount = 2048,
            StoredOutputEnabled = false,
            Temperature = 0.4f
        };

        return ChatClient.CompleteChatStreamingAsync(chatMessages, options);
    }
}
