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
    IAsyncEnumerable<StreamingChatCompletionUpdate> GetCompletionsStreamingAsync(
        ChatMessage[] chatMessages, 
        ChatCompletionOptions? options = null
    );
}

public class AzureOpenAIService : IAzureOpenAIService
{
    private ChatClient ChatClient { get; }
    private EmbeddingClient? EmbeddingClient { get; }
    private IAzureSearchIndexService SearchIndexService { get; }

    public AzureOpenAIService(
        IConfiguration configuration, 
        IAzureSearchIndexService azureSearchIndexService)
    {
        var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
        var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];

        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);

        var client = new AzureOpenAIClient(new Uri(azureOpenaiEndpoint), 
            new ApiKeyCredential(azureOpenaiApiKey));

        EmbeddingClient = client.GetEmbeddingClient(Constants.TextEmbedding3Large);
        ChatClient = client.GetChatClient(Constants.Gpt4o);

        SearchIndexService = azureSearchIndexService;
    }

    public async Task GetRagCompletionsAsync(
        ChatMessage chatMessage, 
        ChatCompletionOptions? options = null)
    {
        // TODO: Hent embeddings for chatMessage
        // Dimensions må settes til 1536, da det er denne størrelsen som brukes i Embedding skill
        var embeddings = await GetEmbeddingsAsync(
            input: chatMessage.Content[0].Text, 
            options: new() { Dimensions = 1536 }
        );
        
        // TODO: Søk etter dokumenter
        
        // TODO: Hent ut relevant info fra dokumentene, og mat dem inn i prompten.

        // TODO: GetCompletionsAsync(...)

        // TODO: Fjern denne når du er ferdig
        throw new NotImplementedException();
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

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> GetCompletionsStreamingAsync(
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

        await foreach (var update in ChatClient.CompleteChatStreamingAsync(chatMessages, options))
        {
            yield return update;
        }
    }
}
