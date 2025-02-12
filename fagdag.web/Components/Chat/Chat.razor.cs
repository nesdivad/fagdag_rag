using System.Text;
using Azure.Search.Documents.Models;
using Fagdag.Utils;
using Fagdag.Web.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OpenAI.Chat;

namespace Fagdag.Web.Components.Chat;

/**

Eksempler på spørringer:

1. Hvordan kan jeg bruke tiden min effektivt når jeg er "på benken" mellom oppdrag?
2. Har Bouvet noen mentorprogrammer eller kan jeg få hjelp til å finne en mentor innen et nytt fagområde?
3. Hvilke sertifiseringer anbefales for å øke mine ferdigheter og muligheter i Bouvet?
4. Hvor kan jeg finne informasjon om Bouvet sine kanalstrategier?
5. Hvordan kan jeg få tilgang til foredrag holdt av kolleger i Bouvet?

*/

public partial class Chat
{
    [Inject]
    internal IAzureOpenAIService? ChatHandler { get; init; }
    [Inject]
    internal IAzureSearchIndexService? SearchHandler { get; init; }
    readonly List<Message> messages = [];
    ElementReference writeMessageElement;
    string? userMessageText;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./Components/Chat/Chat.razor.js");
                await module.InvokeVoidAsync("submitOnEnter", writeMessageElement);
            }
            catch (JSDisconnectedException)
            {
                // Not an error
            }
        }
    }

    static string GetPrompt(string userMessage, string dataContext)
    {
        return 
        $"""
            Du er en chatbot som svarer på spørsmål fra ansatte i konsulentselskapet Bouvet.
            
            Bruk dokumentene under til å svare på spørsmålene:
            <documents>
            {dataContext}
            </documents>

            Her er brukerens spørsmål:
            
            {userMessage}
        """;
    }


    async void SendMessage()
    {
        if (ChatHandler is null || SearchHandler is null) return;

        OpenAI.Chat.ChatMessage[] chatMessages = [
            .. messages.Select<Message, OpenAI.Chat.ChatMessage>(
                x => x.IsAssistant
                    ? new AssistantChatMessage(x.Content)
                    : new UserChatMessage(x.Content))
        ];

        if (!string.IsNullOrWhiteSpace(userMessageText))
        {
            var userMessage = new Message()
            {
                IsAssistant = false,
                Content = userMessageText
            };

            #region [ UI stuff ]

            messages.Add(userMessage);
            userMessageText = null;

            // Add a temporary message that a response is being generated
            Message assistantMessage = new()
            {
                IsAssistant = true,
                Content = ""
            };

            messages.Add(assistantMessage);
            StateHasChanged();

            #endregion

            // TODO: Hent embeddings for userMessage.Content
            // Dimensions må settes til 1536, da det er denne størrelsen som brukes i Embedding skill
            

            // TODO: Søk etter dokumenter ved å bruke SearchHandler


            // TODO: Hent ut relevant info fra dokumentene, og legg dem inn i prompten som en streng.


            // TODO: Lag prompten som en streng
            // Eksempler:
            // https://medium.com/@ajayverma23/the-art-and-science-of-rag-mastering-prompt-templates-and-contextual-understanding-a47961a57e27
            // https://docs.llamaindex.ai/en/v0.10.22/examples/prompts/prompts_rag/


            // TODO: Legg til en ny UserChatMessage i listen 'chatMessages' med prompten du laget
            

            // TODO: Send til AI-modell ved å bruke 'ChatHandler'
            // Det er også mulig å strømme resultatet token for token, som gir en bedre brukeropplevelse.


            // TODO: Append generert svar til assistantMessage.Content, og kall StateHasChanged.
            

            // TODO: remove
            throw new NotImplementedException();
        }
    }
}