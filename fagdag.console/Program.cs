using System.Text;

using Azure.Search.Documents.Indexes.Models;

using Fagdag.Utils;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectre.Console;
using Spectre.Console.Json;

var host = Host.CreateApplicationBuilder(args);
var app = host.Build();
var configuration = app.Services.GetRequiredService<IConfiguration>();

Console.CancelKeyPress += (sender, e) => Console.CursorVisible = true;

string username = string.Empty;
AzureOpenAIService? azureOpenAIService = null;
AzureSearchIndexService? azureSearchIndexService = null;
AzureSearchIndexerService? azureSearchIndexerService = null;

username = TangOgTare.GetOrCreateUsername();
configuration[Constants.Username] = username;

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
        "2. Opprett indeks",
        "3. Opprett indekserer",
        "4. Test hele løsningen!",
        "5. Søk i indeksen",
        "6. Rykk tilbake til start"
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
                "AZURE_COGNITIVESERVICES_API_KEY": "",
                "AZURE_COGNITIVESERVICES_ENDPOINT": "",
                "AZURE_STORAGE_CONNECTION_STRING": "",
                "AZURE_OPENAI_EMBEDDING_ENDPOINT": ""
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

    bool Select()
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
                Index();
                break;
            case 2:
                Indexer();
                break;
            case 3:
                TestIndex();
                break;
            case 4:
                Task.Run(SearchIndex).Wait();
                break;
            case 5:
                return true;
            default:
                AnsiConsole.Clear();
                RenderUsername(username);
                Select();
                break;
        }

        return false;
    }

    AnsiConsole.Clear();
    Information();
    PromptNext();
    RenderSeparator();
    StepZero();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å gå til neste steg.");

    bool @return = false;
    do @return = Select();
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
        AnsiConsole.MarkupLine("Gå til [yellow]Fagdag.Utils.AzureSearchIndexerService.cs[/] og implementér metoden [teal]CreateSkillsetAsync[/].");
        AnsiConsole.MarkupLine("Start med å opprette de individuelle skillsene, før du setter dem sammen i et skillset. Til slutt skal skillsettet deployes ved å bruke metoden [teal]CreateOrUpdateSearchIndexerSkillset[/].");
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

// Oppsett av indeks
static void Index()
{
    void Information()
    {
        AnsiConsole.MarkupLine("[fuchsia]Indeks:[/]");
        AnsiConsole.MarkupLine("Nå skal du sette opp en indeks i [blue]Azure AI Search[/]. En indeks består av søkbart innhold, og brukes som datakilde når du senere skal sette opp RAG-flyten for chatgrensesnittet.");
        AnsiConsole.MarkupLine("En indeks inneholder [lime]dokumenter[/]. Hvert dokument er en søkbar 'enhet' i indeksen, og kan dermed deles opp forskjellig, avhengig av din applikasjon.");
        AnsiConsole.MarkupLine("Hver indeks er definert av et JSON-skjema, som inneholder informasjon om felter i dokumentet og annen metadata.");

        AnsiConsole.MarkupLine("\n [link blue]https://learn.microsoft.com/en-us/azure/search/search-what-is-an-index[/]");
    }

    void Index()
    {
        // Informasjon om indeks-skjema
        AnsiConsole.MarkupLine("[fuchsia]Indeks-skjema:[/]");
        AnsiConsole.MarkupLine("Definisjonen av et dokument i indeksen er laget med klassen [yellow]Fagdag.Utils.Index[/]:\n");
        Panel panel = new(new JsonText(
            """
            {
                "id": "Primærnøkkel for dokumentet",
                "parentId": "Id som vi benytter for å referere til mordokumentet etter vi har splittet datakildene med SplitSkill.",
                "chunk": "Tekstinnholdet etter mordokumentet er splittet opp",
                "vector": "Embeddings for teksten i feltet 'chunk'. Dette feltet har også attributtet 'VectorSearchField', som sier noe om vektorens dimensjon og hvilken søkealgoritme som skal brukes."
            }
            """)
        )
        {
            Header = new PanelHeader("Dokument")
        };
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("[link blue]https://learn.microsoft.com/en-us/azure/search/tutorial-rag-build-solution-index-schema[/]\n");
    }

    void Impl()
    {
        var codePanel = new Panel(new Text(
            """
            public async Task<SearchIndex> CreateOrUpdateSearchIndexAsync()
            {
                ...

                // TODO: Definér skjemaet for indeksen ved å bruke 'FieldBuilder'-klassen for å bygge en liste med search fields av typen 'SearchField'.

                // TODO: Lag en instans av søkeindeksen (SearchIndex), og inkluder:
                // indeksnavn
                // felter (som du lagde i forrige steg)
                // Similarity skal settes til BM25Similarity
                // vectorSearch-instansen

                // TODO: Opprett søkeindeksen i Azure AI Search ved å bruke SearchIndexClient


                // TODO: Returnér searchIndex
                // return searchIndex;
                throw new NotImplementedException();
            }
            """
        ));

        AnsiConsole.MarkupLine("[fuchsia]Implementasjon:[/]");
        AnsiConsole.MarkupLine("Gå til [yellow]Fagdag.Utils.AzureSearchIndexService[/] og implementér metoden [yellow]CreateOrUpdateSearchIndexAsync()[/]");
        AnsiConsole.MarkupLine("Vær obs på at det ligger noe kode her fra før, det er kun TODOs som skal implementeres.");
        AnsiConsole.MarkupLine("Start med å lage en liste av typen 'SearchField' fra indeks-skjema som er definert. Opprett deretter en ny instans av 'SearchIndex', før du til slutt oppretter den i [aqua]Azure AI Search[/] og returnerer 'SearchIndex'-instansen.");

        AnsiConsole.Write(codePanel);
    }

    Information();
    PromptNext();
    RenderSeparator();
    Index();
    PromptNext();
    RenderSeparator();
    Impl();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å fullføre steget.");
}

// Oppsett av indekserer
static void Indexer()
{
    void Information()
    {
        AnsiConsole.MarkupLine("[fuchsia]Indekserer:[/]");
        AnsiConsole.MarkupLine("I dette steget skal vi konfigurere [aqua]AI Search Indexer[/]. Jobben til indekseren er å populere søkeindeksen med data.");
        AnsiConsole.MarkupLine("Den henter først inn datasettet vårt fra en datakilde (som allerede er konfigurert), og så kjører den skillsettet på disse dataene.");
        AnsiConsole.MarkupLine("Når skillsets er kjørt på datasettet, utføres projeksjoner fra output-felter på hvert skill til de korrekte feltene i indeksen, som ble definert i [yellow]Fagdag.Utils.Index[/].");

        AnsiConsole.MarkupLine("\n[link blue]https://learn.microsoft.com/en-us/azure/search/search-indexer-overview[/]");
    }

    void Impl()
    {
        var codePanel = new Panel(
            new Text(
                """
                public async Task<SearchIndexer> CreateOrUpdateIndexerAsync()
                {
                    ...
                    
                    // TODO: Lag en instans av 'IndexingParameters', og sett følgende felter:
                    // MaxFailedItems = -1 (indekserer kjører uansett hvor mange feil du får)
                    // MaxFailedItemsPerBatch = -1 (indekserer kjører uansett hvor mange feil du får)
                    // IndexingParametersConfiguration = []
                    // https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.indexes.models.indexingparameters?view=azure-dotnet

                    // TODO: Legg til konfigurasjon for IndexingParametersConfiguration:
                    // key: "dataToExtract", value: "contentAndMetadata"

                    // TODO: Lag en ny instans av 'SearchIndexer', og inkluder:
                    // indeksnavn
                    // navn på datakilde (DataSourceConnection)
                    // navn på søkeindeks
                    // IndexingParameters som du lagde i forrige steg
                    // Navn på skillset

                    // TODO: Opprett indekserer i Azure AI Search

                    // TODO: Returner indekserer
                    // return indexer;
                    throw new NotImplementedException();
                }
                """
            )
        );

        AnsiConsole.MarkupLine("[fuchsia]Implementasjon:[/]");
        AnsiConsole.MarkupLine("Gå til [yellow]Fagdag.Utils.AzureSearchIndexerService[/] og implementer metoden [yellow]CreateOrUpdateIndexerAsync[/]");
        AnsiConsole.Write(codePanel);
    }

    Information();
    PromptNext();
    RenderSeparator();
    Impl();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å fullføre steget.");
}

// Test indeks, mulighet til å søke på dokumenter i indeksen
void TestIndex()
{
    Task.Run(Test).Wait();
    PromptNext();
}

// Søk i indeksen du har laget
async Task SearchIndex()
{
    async IAsyncEnumerable<string> Search(string searchText)
    {
        var results = azureSearchIndexService.SearchAsync(
            searchText: searchText,
            size: 5
        );

        await foreach (var item in results)
        {
            StringBuilder sb = new();
            sb.AppendLine($"{item.Document.Chunk}\n");
            yield return sb.ToString();
        }
    }

    azureSearchIndexService ??= CreateAzureSearchIndexService(configuration);

    AnsiConsole.MarkupLine("[lime]Søk[/] i søkeindeksen du har bygget opp! Søket returnerer inntil 5 resultater.\n");
    string qa = AnsiConsole.Ask<string>("Skriv inn søkefrasen her:");

    await foreach (var doc in Search(qa))
    {
        AnsiConsole.WriteLine(doc);
        RenderSeparator();
    }

    PromptNext();
}

#endregion

#region [ RAG og generativ AI ]

void RAG()
{
    bool Select()
    {
        string[] choices = [
            "1. Sett opp prompt",
            "2. Sett opp flyt for RAG",
            "3. Test applikasjonen!",
            "4. Rykk tilbake til start"
        ];

        AnsiConsole.Clear();
        RenderUsername(username);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Vennligst velg ditt neste steg:")
                .AddChoices(choices)
        );

        var index = Array.FindIndex(choices, x => x.Equals(choice, StringComparison.OrdinalIgnoreCase));
        switch (index)
        {
            case 0:
                Prompt();
                break;
            case 1:
                RagFlow();
                break;
            case 2:
                TestWebApp();
                break;
            case 3:
                return true;
            default: break;
        }

        return false;
    }

    AnsiConsole.Clear();

    AnsiConsole.MarkupLine("Velkommen til del 2 av fagdagen, hvor du får utforske hvordan data fra søkeindeksen kan tas i bruk i en RAG-pipeline.");
    AnsiConsole.MarkupLine("RAG-løsningen i dette prosjektet er forholdsvis enkel, men du får lære de viktigste trinnene for å ta i bruk ekstern data med en AI-modell.");
    AnsiConsole.MarkupLine("Dersom du ikke ble ferdig med del 1, fortvil ikke; Spør Kristoffer om å bruke den ferdiglagde indeksen slik at du får best mulig utbytte av denne delen også!");

    PromptNext();
    RenderSeparator();

    var appsettings = new JsonText(
        """
        {
            "AZURE_OPENAI_API_KEY": "",
            "AZURE_OPENAI_ENDPOINT": "",
            "AZURE_SEARCH_API_KEY": "",
            "AZURE_SEARCH_ENDPOINT": "",
            "AZURE_COGNITIVESERVICES_API_KEY": "",
            "AZURE_COGNITIVESERVICES_ENDPOINT": "",
            "AZURE_STORAGE_CONNECTION_STRING": "",
            "AZURE_OPENAI_EMBEDDING_ENDPOINT": "",
            "Username": ""
        }
        """
    );

    AnsiConsole.MarkupLine("[fuchsia]Konfigurasjon:[/]");
    AnsiConsole.MarkupLine("Finn filen [yellow]appsettings.json[/] i prosjektet [yellow]fagdag.web[/], og legg inn verdier for følgende variabler: ");
    AnsiConsole.Write(
        new Panel(appsettings)
            .Header("appsettings.json")
            .Collapse()
            .RoundedBorder()
            .BorderColor(Color.Yellow)
    );
    AnsiConsole.MarkupLine("Verdiene ligger i et [yellow]Keeper[/]-dokument, og deles med deg. Verdien [lime]Username[/] ligger i filen [yellow]user.txt[/] i roten av løsningen (sammen med .sln-filen).");

    PromptNext();
    RenderSeparator();

    bool @return = false;
    do @return = Select();
    while (!@return);
}

// Prompt
static void Prompt()
{
    void Information()
    {
        var tipPanel = new Panel(
            new Text(
                """
                1. Start prompten med å definere formålet. Hva vil du at brukeren skal få hjelp til? Prøv å beskrive det så konkret som mulig.
                2. Skriv prompten på det samme språket som du ønsker svaret på. Siden kontekst er på norsk, vil du kanskje få dårligere resultater dersom du blander inn engelsk.
                3. Gi instruksjoner om at AI-modellen kun skal svare basert på konteksten du gir den. Det reduserer sjansen for hallusinasjoner, men øker sjansen for at modellen ikke kan svare hvis konteksten er av dårlig kvalitet.
                4. Håndter tilfeller hvor AI-modellen ikke har kontekst til å svare på spørsmålet, f.eks. ved å fortelle den at den skal svare "Jeg vet ikke" eller "google it!".
                5. Gi et (eller flere) eksempel på spørsmål og svar i prompten, slik at modellen lærer hvilke hvordan den skal svare på oppgaven.
                """
            )
        );

        AnsiConsole.MarkupLine("[fuchsia]Prompt:[/]");
        AnsiConsole.MarkupLine("Alle gode RAG-løsninger trenger en god datakilde, men det er til ingen nytte dersom prompten ikke er satt opp riktig!");
        AnsiConsole.MarkupLine("Det er ingen fasit på hva som utgjør en god eller dårlig prompt, det er forskjellig for hvert brukstilfelle. Her er noen tips som jeg liker å benytte:");
        AnsiConsole.Write(tipPanel);
    }

    void Impl()
    {
        var codePanel = new Panel(
            new Text("static string GetPrompt(string userMessage, string dataContext) { }")
        );
        
        AnsiConsole.MarkupLine("[fuchsia]Implementasjon:[/]");
        AnsiConsole.MarkupLine("Gå til [yellow]Fagdag.Web.Components.Chat.Chat.razor.cs[/] og rediger metoden [yellow]GetPrompt[/]. Metoden har to parametre; [lime]userMessage[/] - brukerens spørsmål, og [lime]dataContext[/] - konteksten du ønsker å gi til modellen.");
        AnsiConsole.Write(codePanel);
    }

    Information();
    PromptNext();
    RenderSeparator();
    Impl();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å gå til neste steg.");
}

// Sett opp flyt for RAG
static void RagFlow()
{
    PromptNext();
}

// Test webapplikasjon
static void TestWebApp()
{
    PromptNext();
}

#endregion

#region [ Tests ]

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
                azureSearchIndexService = CreateAzureSearchIndexService(configuration);
                azureOpenAIService = CreateAzureOpenAIService(configuration);
                azureSearchIndexerService = CreateAzureSearchIndexerService(configuration);

                Sleep();

                AnsiConsole.MarkupLine("Test av konfigurasjon i appsettings.json :check_mark_button:");
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

async Task<bool> TestStepOne()
{
    var successful = false;

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Default)
        .StartAsync("Oppretter indeks...", async ctx =>
        {
            try
            {
                ArgumentNullException.ThrowIfNull(azureSearchIndexService);
                var index = await azureSearchIndexService.CreateOrUpdateSearchIndexAsync();
                Sleep();
                AnsiConsole.MarkupLineInterpolated($"Test av indeks vellykket! Indeks med navn {index.Name} er opprettet. :check_mark_button:");
                successful = true;
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLineInterpolated($"Noe gikk galt under oppretting av indeksen.\n{e.Message}");
            }
        });

    return successful;
}

async Task<bool> TestStepTwo()
{
    bool successful = false;
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Default)
        .StartAsync("Oppretter skillsets...", async ctx =>
        {
            try
            {
                ArgumentNullException.ThrowIfNull(azureSearchIndexerService);
                var skillset = await azureSearchIndexerService.CreateSkillsetAsync();
                Sleep();
                AnsiConsole.MarkupLine($"Test av skillsets vellykket! Skillset med navn {skillset.Name} er opprettet. :check_mark_button:");
                successful = true;
            }
            catch (Exception)
            {
                AnsiConsole.MarkupLine($"[red]Noe gikk galt under oppretting av skillsets.[/]\n");
            }
        });

    return successful;
}


async Task<bool> TestStepThree()
{
    var successful = false;

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Default)
        .StartAsync("Oppretter indekserer...", async ctx =>
        {
            ArgumentNullException.ThrowIfNull(azureSearchIndexerService);
            SearchIndexer? indexer = null;
            try
            {
                indexer = await azureSearchIndexerService.CreateOrUpdateIndexerAsync();

                AnsiConsole.MarkupLineInterpolated($"Test av indekserer vellykket! Indekserer med navn {indexer.Name} er opprettet. :check_mark_button:");
                successful = true;
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLineInterpolated($"Noe gikk galt under oppretting av indekserer.\n{e.Message} {e.StackTrace}");
            }

            try
            {
                ArgumentNullException.ThrowIfNull(indexer);
                ctx.Status("Sjekker status til indekserer ...");
                IndexerExecutionResult? indexerStatus = default;

                do
                {
                    Sleep(milliseconds: 4000);
                    indexerStatus = await azureSearchIndexerService.GetIndexerStatus(indexer);
                    AnsiConsole.MarkupLineInterpolated($"Status for indekserer: {indexerStatus?.Status.ToString()}");
                }
                while (indexerStatus?.Status == IndexerExecutionStatus.InProgress);
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLineInterpolated($"{e.Message} {e.StackTrace}");
                AnsiConsole.MarkupLine("[red]Noe gikk galt under sjekk av status på indekserer.[/]");
            }
        });

    return successful;
}

async Task Test()
{
    var stepZeroSuccess = TestStepZero();
    if (!stepZeroSuccess) return;

    var stepOneSuccess = await TestStepOne();
    if (!stepOneSuccess) return;

    var stepTwoSuccess = await TestStepTwo();
    if (!stepTwoSuccess) return;

    var stepThreeSuccess = await TestStepThree();
    if (!stepThreeSuccess) return;
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
    return new(configuration);
}

static AzureSearchIndexService CreateAzureSearchIndexService(IConfiguration configuration)
    => new(configuration);

static AzureSearchIndexerService CreateAzureSearchIndexerService(IConfiguration configuration)
    => new(configuration);

void Sleep(long milliseconds = 500) => Thread.Sleep(TimeSpan.FromMilliseconds(milliseconds));

#endregion
