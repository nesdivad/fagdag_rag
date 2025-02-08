using Fagdag.Utils;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectre.Console;
using Spectre.Console.Json;

var host = Host.CreateApplicationBuilder(args);
var app = host.Build();
var configuration = app.Services.GetRequiredService<IConfiguration>();


string username = string.Empty;
string[] startChoices = [
    "1. Sett opp dataflyt og søkeindeks",
    "2. Sett opp RAG og prompts for generativ AI"
];

AzureOpenAIService? azureOpenAIService;
AzureSearchIndexerService? azureSearchIndexerService;


Start(startChoices);

void Start(string[] choices)
{
    while (true)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("Velkommen til fagdag om [green]RAG![/]\n");
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Vennligst velg ditt neste steg:")
                .AddChoices(choices)
        );


        var index = Array.FindIndex(choices, x => x.Equals(choice, StringComparison.OrdinalIgnoreCase));
        switch (index)
        {
            case 0:
                DataFlow(configuration);
                break;
            case 1:
            default:
                RAG();
                break;
        }
    }
}

#region [ Dataflyt og indeksering ]

// Introduksjon til prosjektet, oppsett av username
void DataFlow(IConfiguration configuration)
{
    string[] choices = [
        "1. Sett opp innstillinger",
        "2. Start implementasjon",
        "3. Lag embeddings",
        "4. Lagre embeddings i database",
        "5. [lime]Test hele løsningen![/]"
    ];

    var appsettings = new JsonText(
        """
        {
            "AZURE_OPENAI_API_KEY": "",
            "AZURE_OPENAI_ENDPOINT": "",
            "AZURE_SEARCH_API_KEY": "",
            "AZURE_SEARCH_ENDPOINT": "",
            "AZURE_STORAGE_CONNECTION_STRING": ""
        }
        """
    );

    void Information()
    {
        AnsiConsole.MarkupLine("Velkommen til første del av fagdagen om generativ AI med [green]RAG![/]");
        AnsiConsole.MarkupLine("I denne delen skal vi fokusere på å indeksere datakilden slik at den kan gjøres søkbar for løsningen vi skal bygge.");

        AnsiConsole.MarkupLine("\n[fuchsia]Datakilde:[/]");
        AnsiConsole.MarkupLine("Datakilden vår består av tekstlig datadumper fra Bouvets personalhåndbok og diverse informasjon som ligger under området 'Meg som ansatt'.");
        AnsiConsole.MarkupLine("Målet er at du skal sitte igjen med en løsning som kan brukes til å spørre om alt du måtte lure på som ansatt i Bouvet. Eksempler på spørsmål kan være hvilke [green]goder[/] du kan benytte deg av, hva du må gjøre dersom du blir [red]sykemeldt[/] m.m.");

        AnsiConsole.MarkupLine("\n[fuchsia]Teknologi:[/]");
        AnsiConsole.MarkupLine("I denne løsningen benytter vi AI-modeller fra OpenAI, som kjøres i Azure. Modellene benyttes til flere ting, som å svare på spørsmål eller lage [aqua]embeddings[/] (vi kommer tilbake til dette) av datakildene.");
        AnsiConsole.MarkupLine("I tillegg benytter vi tjenesten [aqua]Azure AI Search[/], som er en komplett tjeneste for både indeksering og søking i data.");

    }

    void StepZero()
    {
        AnsiConsole.MarkupLine("[fuchsia]Konfigurasjon:[/]");
        AnsiConsole.MarkupLine("Finn filen [yellow]appsettings.json[/] i prosjektet [yellow]fagdag.embeddings[/], og legg inn verdier for følgende variabler: ");
        AnsiConsole.Write(
            new Panel(appsettings)
                .Header("appsettings.json")
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Yellow)
        );
        AnsiConsole.MarkupLine("Verdiene ligger i et [yellow]Keeper[/]-dokument, og deles med deg.");
    }

    bool TestConfiguration()
    {
        bool successful = false;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .Start("Kjører test av konfigurasjon...", ctx =>
            {
                AnsiConsole.Write("\n");
                try
                {
                    username = CreateOrRetrieveUsername();
                    azureOpenAIService = CreateAzureOpenAIService(configuration);
                    azureSearchIndexerService = CreateAzureSearchIndexerService(configuration, username);

                    Thread.Sleep(TimeSpan.FromMilliseconds(500));

                    AnsiConsole.MarkupLine("[green]Test av konfigurasjon vellykket![/]");
                    successful = true;
                }
                catch (ArgumentException ex)
                {
                    AnsiConsole.MarkupLine($"Testen gikk ikke som forventet!\n[red]{ex.Message}[/]");
                    successful = false;
                }
                catch (UriFormatException ex)
                {
                    AnsiConsole.MarkupLine($"Vennligst sjekk formatet på endepunktene!\n[red]{ex.Message}[/]");
                    successful = false;
                }
            });

        return successful;
    }

    void Select()
    {
        var step = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Velg ditt neste steg:")
                .AddChoices(choices)
        );

        var index = Array.FindIndex(choices, x => x.Equals(step, StringComparison.OrdinalIgnoreCase));
        switch (index)
        {
            case 0:
                TextProcessing();
                break;
            case 1:
                IndexAndIndexer();
                break;
            case 2:
                RunIndexer();
                break;
            case 3:
                TestIndex();
                break;
            default:
                AnsiConsole.Clear();
                RenderUsername(username);
                Select();
                break;
        }
    }

    AnsiConsole.Clear();
    Information();
    PromptNext();
    RenderSeparator();
    StepZero();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å gå til neste steg.");
    AnsiConsole.Clear();
    RenderUsername(username);

    bool @return = false;
    do Select();
    while (!@return);
}

// Prosessering av tekst
// Oppsett av skills og skillset
static void TextProcessing()
{
    PromptNext();
}

// Oppsett av indeks og indekserer
static void IndexAndIndexer()
{
    PromptNext();
}

// Kjøre indeksering, og alt rundt
static void RunIndexer()
{
    PromptNext();
}

// Test indeks, mulighet til å søke på dokumenter i indeksen
static void TestIndex()
{
    PromptNext();
}

// Første del er over, over til fase 2!
static void Break()
{
    PromptNext();
}

#endregion

#region [ RAG og generativ AI ]

static void RAG()
{

}

// Introduksjon til fase 2
static void HelloAgain()
{

}

// Prompt
static void Prompt()
{

}

// Sett opp flyt for RAG
static void RagFlow()
{

}

// Koble sammen flyt for RAG og prompt, sende til AI Services
static void ConnectStuff()
{

}

static void Goodbye()
{

}

#endregion

#region [ Helpers ]

static void PromptNext(string prompt = "\nTrykk [teal]Enter[/] for å gå videre.")
{
    ConsoleKeyInfo cki;
    AnsiConsole.MarkupLine(prompt);

    do cki = Console.ReadKey(intercept: true);
    while (cki.Key is not ConsoleKey.Enter);
}

static void RenderUsername(string username)
{
    Rule rule = new($"[aqua]{username}[/]");
    rule.RightJustified();
    AnsiConsole.Write(rule);
}

static void RenderSeparator() => AnsiConsole.Write(new Rule().HeavyBorder());

static AzureOpenAIService CreateAzureOpenAIService(IConfiguration configuration)
{
    var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
    var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];

    ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
    ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);

    var resourceUri = new Uri(azureOpenaiEndpoint);

    return new(
        endpoint: resourceUri,
        apiKey: azureOpenaiApiKey,
        deploymentName: Constants.Gpt4o,
        embeddingDeploymentName: Constants.TextEmbedding3Large,
        new() { NetworkTimeout = TimeSpan.FromSeconds(30) }
    );
}

static AzureSearchIndexerService CreateAzureSearchIndexerService(IConfiguration configuration, string username)
{
    var azureSearchApiKey = configuration[Constants.AzureSearchApiKey];
    var azureSearchEndpoint = configuration[Constants.AzureSearchEndpoint];
    var azureOpenaiApiKey = configuration[Constants.AzureOpenAIApiKey];
    var azureOpenaiEndpoint = configuration[Constants.AzureOpenAIEndpoint];
    var azureStorageConnectionString = configuration[Constants.AzureStorageConnectionString];

    ArgumentException.ThrowIfNullOrEmpty(azureSearchApiKey);
    ArgumentException.ThrowIfNullOrEmpty(azureSearchEndpoint);
    ArgumentException.ThrowIfNullOrEmpty(azureOpenaiApiKey);
    ArgumentException.ThrowIfNullOrEmpty(azureOpenaiEndpoint);
    ArgumentException.ThrowIfNullOrEmpty(azureStorageConnectionString);

    return new(
        username: username,
        searchApiKey: azureSearchApiKey,
        searchEndpoint: azureSearchEndpoint,
        aiServicesApiKey: azureOpenaiApiKey,
        aiServicesEndpoint: azureOpenaiEndpoint,
        storageConnectionString: azureStorageConnectionString
    );
}

static string CreateOrRetrieveUsername()
{
    var filename = "user.txt";
    var existingUser = File.Exists(filename);
    if (existingUser)
    {
        return File.ReadAllText(filename);
    }
    else
    {
        var rand = new Random();
        var user = $"u_{rand.Next(0, 1_000_000)}";
        File.WriteAllText(filename, user);
        return user;
    }
}

#endregion

public static class Constants
{
    public const string AzureOpenAIEndpoint = "AZURE_OPENAI_ENDPOINT";
    public const string AzureOpenAIApiKey = "AZURE_OPENAI_API_KEY";
    public const string AzureSearchEndpoint = "AZURE_SEARCH_ENDPOINT";
    public const string AzureSearchApiKey = "AZURE_SEARCH_API_KEY";
    public const string AzureStorageConnectionString = "AZURE_STORAGE_CONNECTION_STRING";
    public const string Gpt4o = "gpt-4o";
    public const string TextEmbedding3Large = "text-embedding-3-large";
}