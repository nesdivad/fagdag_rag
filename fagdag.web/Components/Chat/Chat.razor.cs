using Fagdag.Web.Model;
using Fagdag.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fagdag.Web.Components.Chat;

public partial class Chat
{
    [Inject]
    internal ChatService? ChatHandler { get; init; }
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

    async void SendMessage()
    {
        if (ChatHandler is null) { return; }

        if (!string.IsNullOrWhiteSpace(userMessageText))
        {
            // Add the user's message to the UI
            // TODO: Don't rely on "magic strings" for the Role
            messages.Add(new Message() {
                IsAssistant = false,
                Content = userMessageText
                });
                
            userMessageText = null;

            ChatRequest request = new ChatRequest(messages);

            // Add a temporary message that a response is being generated
            Message assistantMessage = new Message() {
                IsAssistant = true,
                Content = ""
            };
            
            messages.Add(assistantMessage);
            StateHasChanged();

            var message = await ChatHandler.Chat(request);

            // await foreach (var chunk in chunks)
            // {
            //     assistantMessage.Content += chunk;
            //     StateHasChanged();
            // }

            assistantMessage.Content += message.Content;
            StateHasChanged();
        }
    }
}