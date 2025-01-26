using Azure.Identity;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Google.Protobuf.WellKnownTypes;
using OpenAI.Embeddings;
using Azure.Core;

namespace Fagdag.Utils;

public interface IAzureOpenAIService
{

}

public class AzureOpenAIService : IAzureOpenAIService
{
    private ChatClient _chatClient { get; }
    private EmbeddingClient? _embeddingClient { get; }

    /**
     * <summary>Initialize the chat client with an API key</summary>
     */
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
}