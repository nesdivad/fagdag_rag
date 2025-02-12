using Fagdag.Utils;
using Fagdag.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();
builder.Services.AddScoped<IAzureSearchIndexService, AzureSearchIndexService>();

var app = builder.Build();

var configuration = app.Configuration;
var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];

ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();