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
AzureOpenAIService? azureOpenAIService;
AzureSearchIndexerService? azureSearchIndexerService;

Start();

void Start()
{
    string[] choices = [
        "1. Sett opp dataflyt og søkeindeks",
        "2. Sett opp RAG og prompts for generativ AI"
    ];

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
        "1. Konfigurer skills og skillset",
        "2. Opprett indekserer",
        "3. Lag embeddings",
        "4. Lagre embeddings i database",
        "5. [lime]Test hele løsningen![/]"
    ];

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
        
        AnsiConsole.MarkupLine("[fuchsia]Konfigurasjon:[/]");
        AnsiConsole.MarkupLine("Finn filen [yellow]appsettings.json[/] i prosjektet [yellow]fagdag.console[/], og legg inn verdier for følgende variabler: ");
        AnsiConsole.Write(
            new Panel(appsettings)
                .Header("appsettings.json")
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Yellow)
        );
        AnsiConsole.MarkupLine("Verdiene ligger i et [yellow]Keeper[/]-dokument, og deles med deg.");
    }

    bool TestStepZero()
    {
        bool successful = false;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Default)
            .Start("Kjører test av konfigurasjon...", ctx =>
            {
                AnsiConsole.Write("\n");
                try
                {
                    azureOpenAIService = CreateAzureOpenAIService(configuration);
                    azureSearchIndexerService = CreateAzureSearchIndexerService(configuration);

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
        AnsiConsole.Clear();
        RenderUsername(username);

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
    username = CreateOrRetrieveUsername();
    configuration[Constants.Username] = username;

    bool @return = false;
    do Select();
    while (!@return);
}

// Prosessering av tekst
// Oppsett av skills og skillset
void TextProcessing()
{
    void Information()
    {
        AnsiConsole.MarkupLine("[fuchsia]Tekstprosessering:[/]");
        AnsiConsole.MarkupLine("I denne delen skal du lære å sette opp en pipeline for prosessering av tekst, fra rå data i markdown-format til søkbare indekserte dokumenter.");
        AnsiConsole.MarkupLine("Den tekniske termen for en pipeline i [aqua]Azure AI Search[/] er et [aqua]skillset[/]. Et [aqua]skillset[/] består av ett eller flere [lime]skills[/].");
        AnsiConsole.MarkupLine("Et [lime]skill[/] består av funksjonalitet som beriker søkedokumentet med informasjon. Dette kan være funksjonalitet som oversetter tekst, fjerner sensitiv informasjon, splitter dokumenter opp i mindre biter m.m.");
        AnsiConsole.MarkupLine("Det er mulig å lage egne skills, men i denne løsningen skal vi bare bruke skills fra Microsoft sitt bibliotek.");
        AnsiConsole.MarkupLine("Mer dokumentasjon om skillsets finnes her: [link]https://learn.microsoft.com/en-us/azure/search/cognitive-search-working-with-skillsets[/]");
    }

    void Skills()
    {
        AnsiConsole.MarkupLine("[fuchsia]Skillsets:[/]");
        AnsiConsole.MarkupLine("I dette steget skal skillsettet implementeres. For å holde det relativt enkelt er det valgt ut 3 skills som skal benyttes i settet, og hele flyten kan da se slik ut:");
        AnsiConsole.MarkupLine("\n[red]Rå data[/] | [lime]PII Detection[/] | [lime]Split skill[/] | [lime]Embedding skill[/] | [blue]Søkeklart dokument![/]\n");
        AnsiConsole.MarkupLine("[lime underline]PII Detection[/]: Finner tekst som inneholder personlig identifiserbar informasjon og maskerer den.");
        AnsiConsole.MarkupLine("[lime underline]Split skill[/]: Splitter dokumenter opp i mindre deler.");
        AnsiConsole.MarkupLine("[lime underline]Embedding skill[/]: Lager embeddings av teksten i dokumentet.\n");

        AnsiConsole.MarkupLine("[link blue]https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-pii-detection[/]");
        AnsiConsole.MarkupLine("[link blue]https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-textsplit[/]");
        AnsiConsole.MarkupLine("[link blue]https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-azure-openai-embedding[/]");
    }

    void Impl()
    {
        AnsiConsole.MarkupLine("[fuchsia]Implementasjon:[/]");
        AnsiConsole.MarkupLine("Gå til [yellow]Fagdag.Utils.AzureSearchIndexerService.cs[/] og implementér metoden [purple]CreateSkillsetAsync[/].");
        AnsiConsole.MarkupLine("Start med å opprette de individuelle skillsene, før du setter dem sammen i et skillset. Til slutt skal skillsettet deployes ved å bruke metoden [purple]CreateOrUpdateSearchIndexerSkillset[/].");
        AnsiConsole.MarkupLine("\n[lime]PS:[/] Det er laget implementasjoner for individuelle skills nederst i [yellow]Fagdag.Utils.AzureSearchIndexerService.cs[/]");

        var codePanel = new Panel(new Text(
            """
            public async Task<SearchIndexerSkillset> CreateSkillsetAsync()
            {
                // TODO: Dekonstruer API-nøkkel for AI Services

                // TODO: Opprett en instans av hver skill du ønsker å bruke

                // TODO: Lag en liste med alle skills

                // TODO: Deploy skillsettet til ressursen i Azure    

                // TODO: Fjern denne når implementasjonen er klar
                throw new NotImplementedException();
            }
            """
        ));

        AnsiConsole.Write(codePanel);
    }

    AnsiConsole.Clear();
    RenderUsername(username);

    Information();
    PromptNext();

    RenderSeparator();
    Skills();
    PromptNext();

    RenderSeparator();
    Impl();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å fullføre steget.");
}

// Oppsett av indeks og indekserer
static void IndexAndIndexer()
{
    void Information()
    {
        AnsiConsole.MarkupLine("[fuchsia]Indekserer:[/]");
        AnsiConsole.MarkupLine("I dette steget skal vi konfigurere [aqua]AI Search Indexer[/]. Jobben til indekseren er å populere søkeindeksen med data.");
        AnsiConsole.MarkupLine("Den henter først inn datasettet vårt fra en datakilde (som allerede er konfigurert), og så kjører den skillsettet på disse dataene.");
        AnsiConsole.MarkupLine("\n[link blue]https://learn.microsoft.com/en-us/azure/search/search-indexer-overview[/]");
    }

    void Indexer()
    {
        AnsiConsole.MarkupLine("");
    }

    void Impl()
    {
    }
    
    Information();
    PromptNext();
    RenderSeparator();
    Indexer();
    PromptNext();
    RenderSeparator();
    Impl();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å fullføre steget.");
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

static AzureSearchIndexerService CreateAzureSearchIndexerService(IConfiguration configuration)
{
    return new(configuration);
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
