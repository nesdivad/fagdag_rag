using Fagdag.Utils;
using Fagdag.Web.Components;
using Fagdag.Web.Model;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
var configuration = app.Configuration;

var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];

ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);

// Configure AI related features
var chatService = new AzureOpenAIService(
    endpoint: new Uri(azureOpenaiEndpoint),
    apiKey: azureOpenaiApiKey,
    deploymentName: "gpt-4o",
    embeddingDeploymentName: "text-embedding-3-large",
    options: new()
);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapPost("/chat", async (ChatRequest request) => 
{
    ChatMessage[] chatMessages = [..request.Messages.Select(x => new UserChatMessage(x.Content))];
    await chatService.GetCompletionsAsync(chatMessages);
}); // Uncomment for a non-streaming response
// app.MapPost("/chat/stream", (ChatRequest request, ChatService chatHandler) => chatHandler.Stream(request));

app.Run();