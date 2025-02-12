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

    /**
     *<summary>Lag embeddings av en tekststreng</summary>
     *<param name="input">Tekststrengen</param>
     *<param name="options">Innstillinger for antall dimensjoner. Anbefaler ikke å bruke denne.</param>
     *<returns>Minnesegment med floats som er readonly</returns>
     */
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

    /**
     *<summary>Send spørsmål til AI-modellen GPT-4o</summary>
     *<param name="chatMessages">liste med meldinger</param>
     *<param name="options">Innstillinger som temperatur, maks antall tokens m.m.</param>
     *<returns>Resultat med alle tokens på én gang</returns>
     */
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

    /**
     *<summary>Send spørsmål til AI-modellen GPT-4o</summary>
     *<param name="chatMessages">liste med meldinger</param>
     *<param name="options">Innstillinger som temperatur, maks antall tokens m.m.</param>
     *<returns>Resultatet som en strøm av tokens</returns>
     */
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
