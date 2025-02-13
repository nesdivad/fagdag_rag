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

Console.CancelKeyPress += (sender, e) => Console.CursorVisible = true;

string username = TangOgTare.GetOrCreateUsername();
ArgumentException.ThrowIfNullOrEmpty(username);

var configuration = app.Services.GetRequiredService<IConfiguration>();
AzureOpenAIService? azureOpenAIService = null;
AzureSearchIndexService? azureSearchIndexService = null;
AzureSearchIndexerService? azureSearchIndexerService = null;

AnsiMarkup m = AnsiConsole.MarkupLine;

Start();

void Start()
{
    string[] choices = [
        "1. Sett opp dataflyt og søkeindeks",
        "2. Sett opp prompt og RAG-flyt for generativ AI",
        "3. Avslutt"
    ];

    while (true)
    {
        AnsiConsole.Clear();
        m("Velkommen til fagdag om [green]RAG![/]\n");
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
                RAG();
                break;
            default:
                Exit();
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
        m("Velkommen til første del av fagdagen om generativ AI med [green]RAG![/]");
        m("I denne delen skal vi fokusere på å indeksere datakilden slik at den kan gjøres søkbar for løsningen vi skal bygge.");

        m("\n[fuchsia]Datakilde:[/]");
        m("Datakilden vår består av tekstlig datadumper fra Bouvets personalhåndbok, informasjon som ligger under området 'Meg som ansatt' og diverse annen informasjon fra minside.");
        m("Får du ikke svar på spørsmålet ditt, er sjansen stor for at informasjonen ikke er lagt inn. Dette kan du sjekke i steg 5 - [blue]Søk i indeksen[/].");
        m("Målet er at du skal sitte igjen med en løsning som kan brukes til å spørre om ting du måtte lure på som ansatt i Bouvet. Eksempler på spørsmål kan være hvilke [green]goder[/] du kan benytte deg av, hva du må gjøre dersom du blir [red]sykemeldt[/] m.m.");

        m("\n[fuchsia]Teknologi:[/]");
        m("I denne løsningen benytter vi AI-modeller fra OpenAI, som kjøres i Azure. Modellene benyttes til flere ting, som å svare på spørsmål eller lage [aqua]embeddings[/] (vi kommer tilbake til dette) av datakildene.");
        m("I tillegg benytter vi tjenesten [aqua]Azure AI Search[/], som er en komplett tjeneste for både indeksering og søking i data.");

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

        m("[fuchsia]Konfigurasjon:[/]");
        m("Finn filen [yellow]appsettings.json[/] i prosjektet [yellow]fagdag.console[/], og legg inn verdier for følgende variabler: ");
        AnsiConsole.Write(
            new Panel(appsettings)
                .Header("appsettings.json")
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Yellow)
        );
        m("Verdiene ligger i et [yellow]Keeper[/]-dokument, og deles med deg.");
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
        m("[fuchsia]Tekstprosessering:[/]");
        m("I denne delen skal du lære å sette opp en pipeline for prosessering av tekst, fra rå data i markdown-format til søkbare indekserte dokumenter.");
        m("Den tekniske termen for en pipeline i [aqua]Azure AI Search[/] er et [aqua]skillset[/]. Et [aqua]skillset[/] består av ett eller flere [lime]skills[/].");
        m("Et [lime]skill[/] består av funksjonalitet som beriker søkedokumentet med informasjon. Dette kan være funksjonalitet som oversetter tekst, fjerner sensitiv informasjon, splitter dokumenter opp i mindre biter m.m.");
        m("Det er mulig å lage egne skills, men i denne løsningen skal vi kun bruke skills fra Microsoft sitt bibliotek.");
        m("Mer dokumentasjon om skillsets finnes her: [link blue]https://learn.microsoft.com/en-us/azure/search/cognitive-search-working-with-skillsets[/]");
    }

    void Skills()
    {
        m("[fuchsia]Skillsets:[/]");
        m("I dette steget skal skillsettet implementeres. I denne løsningen er det valgt ut 2 skills som skal benyttes i settet, og flyten kan da se slik ut:");
        m("\n[red]Rå data[/] | [lime]Split skill[/] | [lime]Embedding skill[/] | [blue]Søkeklart dokument![/]\n");
        m("[lime underline]Split skill[/]: Splitter dokumenter opp i mindre deler.");
        m("[lime underline]Embedding skill[/]: Lager embeddings av teksten i dokumentet.\n");

        m("[link blue]https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-textsplit[/]");
        m("[link blue]https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-azure-openai-embedding[/]");
    }

    void Impl()
    {
        m("[fuchsia]Implementasjon:[/]");
        m("Gå til [yellow]Fagdag.Utils.AzureSearchIndexerService.cs[/] og implementér metoden [teal]CreateSkillsetAsync[/].");
        m("Start med å opprette hvert individuelle skill, før du setter dem sammen i et skillset. Til slutt skal skillsettet deployes ved å bruke metoden [teal]CreateOrUpdateSearchIndexerSkillset[/].");
        m("\nPS: Det er laget implementasjoner for individuelle skills nederst i [yellow]Fagdag.Utils.AzureSearchIndexerService.cs[/].");

        var codePanel = new Panel(new Text(
            """
            public async Task<SearchIndexerSkillset> CreateSkillsetAsync()
            {
                ...

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
void Index()
{
    void Information()
    {
        m("[fuchsia]Indeks:[/]");
        m("Nå skal du sette opp en indeks i [blue]Azure AI Search[/]. En indeks består av søkbart innhold, og brukes som datakilde når du senere skal sette opp RAG-flyten for chatgrensesnittet.");
        m("En indeks inneholder [lime]dokumenter[/]. Hvert dokument er en søkbar 'enhet' i indeksen, og kan dermed deles opp forskjellig, avhengig av din applikasjon.");
        m("Hver indeks er definert av et JSON-skjema, som inneholder informasjon om felter i dokumentet og annen metadata.");

        m("\n[link blue]https://learn.microsoft.com/en-us/azure/search/search-what-is-an-index[/]");
    }

    void Index()
    {
        // Informasjon om indeks-skjema
        m("[fuchsia]Indeks-skjema:[/]");
        m("Definisjonen av et dokument i indeksen er laget med klassen [yellow]Fagdag.Utils.Index[/]:\n");
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
        m("[link blue]https://learn.microsoft.com/en-us/azure/search/tutorial-rag-build-solution-index-schema[/]\n");
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

        m("[fuchsia]Implementasjon:[/]");
        m("Gå til [yellow]Fagdag.Utils.AzureSearchIndexService[/] og implementér metoden [teal]CreateOrUpdateSearchIndexAsync()[/]");
        m("Vær obs på at det ligger noe kode her fra før, det er kun TODOs som skal implementeres.");
        m("Start med å lage en liste av typen 'SearchField' fra indeks-skjema som er definert. Opprett deretter en ny instans av 'SearchIndex', før du til slutt oppretter den i [aqua]Azure AI Search[/] og returnerer 'SearchIndex'-instansen.");

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
void Indexer()
{
    void Information()
    {
        m("[fuchsia]Indekserer:[/]");
        m("I dette steget skal vi konfigurere [aqua]AI Search Indexer[/]. Jobben til indekseren er å populere søkeindeksen med data.");
        m("Den henter først inn datasettet vårt fra en datakilde (som allerede er konfigurert), og så kjører den skillsettet på disse dataene.");
        m("Når skillsets er kjørt på datasettet, utføres projeksjoner fra output-felter på hvert skill til de korrekte feltene i indeksen, som ble definert i [yellow]Fagdag.Utils.Index[/].");

        m("\n[link blue]https://learn.microsoft.com/en-us/azure/search/search-indexer-overview[/]");
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

        m("[fuchsia]Implementasjon:[/]");
        m("Gå til [yellow]Fagdag.Utils.AzureSearchIndexerService[/] og implementer metoden [yellow]CreateOrUpdateIndexerAsync[/]");
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

    m("[lime]Søk[/] i søkeindeksen du har bygget opp! Søket returnerer inntil 5 resultater.\n");
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

    m("Velkommen til del 2 av fagdagen, hvor du får utforske hvordan data fra søkeindeksen kan tas i bruk i en RAG-pipeline.");
    m("RAG-løsningen i dette prosjektet er forholdsvis enkel, men du får lære de viktigste trinnene for å ta i bruk ekstern data med en AI-modell.");
    m("Dersom du ikke ble ferdig med del 1, fortvil ikke; Spør Kristoffer om å bruke den ferdiglagde indeksen slik at du får best mulig utbytte av denne delen også!");

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
            "AZURE_OPENAI_EMBEDDING_ENDPOINT": ""
        }
        """
    );

    m("[fuchsia]Konfigurasjon:[/]");
    m("Finn filen [yellow]appsettings.json[/] i prosjektet [yellow]fagdag.web[/], og legg inn verdier for følgende variabler: ");
    AnsiConsole.Write(
        new Panel(appsettings)
            .Header("appsettings.json")
            .Collapse()
            .RoundedBorder()
            .BorderColor(Color.Yellow)
    );
    m("Verdiene ligger i et [yellow]Keeper[/]-dokument, og deles med deg.");

    PromptNext();
    RenderSeparator();

    bool @return = false;
    do @return = Select();
    while (!@return);
}

// Prompt
void Prompt()
{
    void Information()
    {
        var tipPanel = new Panel(
            new Text(
                """
                1. Start prompten med å definere formålet. Hva vil du at AI-modellen skal hjelpe brukeren med? Prøv å beskrive det så konkret som mulig.
                
                2. Skriv prompten på det samme språket som du ønsker svaret på. Siden kontekst (datakilden) er på norsk, vil du kanskje få dårligere resultater dersom du blander inn engelsk.
                
                3. Gi instruksjoner om at AI-modellen kun skal svare basert på konteksten du gir den. Det reduserer sjansen for hallusinasjoner, men øker sjansen for at modellen ikke kan svare hvis konteksten ikke har informasjonen som trengs for å svare på spørsmålet.
                
                4. Håndter tilfeller hvor AI-modellen ikke har kontekst til å svare på spørsmålet, f.eks. ved å fortelle den at den skal svare "Jeg vet ikke" eller "google it!".
                
                5. Gi et (eller flere) eksempel på spørsmål og svar i prompten, slik at modellen lærer hvilke hvordan den skal svare på oppgaven. Dette punktet kommer litt an på hvilken type løsning du lager, og det er ikke sikkert du får noe utbytte av å gjøre det i dette tilfelle.
                """
            )
        );

        m("[fuchsia]Prompt:[/]");
        m("Alle RAG-løsninger trenger en god datakilde, men det er til liten nytte dersom prompten ikke er satt opp riktig!");
        m("Det er ingen fasit på hva som utgjør en god eller dårlig prompt, det er forskjellig for hvert brukstilfelle.");
        m("\nHer er noen tips som jeg liker å benytte:");
        AnsiConsole.Write(tipPanel);
    }

    void Impl()
    {
        var codePanel = new Panel(
            new Text("static string GetPrompt(string userMessage, string dataContext) { }")
        );
        
        m("[fuchsia]Implementasjon:[/]");
        m("Gå til [yellow]Fagdag.Web.Components.Chat.Chat.razor.cs[/] og rediger metoden [yellow]GetPrompt[/]. Metoden har to parametre; \n[lime]userMessage[/] - brukerens spørsmål\n[lime]dataContext[/] - konteksten du ønsker å gi til modellen.");
        AnsiConsole.Write(codePanel);
    }

    Information();
    PromptNext();
    RenderSeparator();
    Impl();
    PromptNext(prompt: "\nTrykk [teal]Enter[/] for å gå til neste steg.");
}

// Sett opp flyt for RAG
void RagFlow()
{
    var pipeline = new Panel(
        new Text(
            """
            1. Lag embeddings av spørsmålet med embedding-modell fra Azure OpenAI
            2. Søk etter dokumenter i Azure AI Search
            3. Lag prompt som inneholder spørsmål og kontekst fra dokumentsøket
            4. Generer med AI-modell fra Azure OpenAI
            """
        )
    );
    
    m("[fuchsia]Flyt for RAG:[/]");
    m("I dette steget skal du sette sammen flyten for RAG - fra brukerens spørsmål kommer inn til ferdig generert svar er returnert.");
    m("Flyten for RAG kan se slik ut:\n");
    AnsiConsole.Write(pipeline);

    PromptNext();
    RenderSeparator();

    var codePanel = new Panel(
        new Text(
            """
            async void SendMessage()
            {
                ...

                if (!string.IsNullOrWhiteSpace(userMessageText))
                {
                    var userMessage = new Message()
                    {
                        IsAssistant = false,
                        Content = userMessageText
                    };

                    ...

                    // TODO: Hent embeddings for userMessage.Content med 'ChatHandler'
                    // Dimensions må settes til 1536, da det er denne størrelsen som brukes i Embedding skill

                    // TODO: Søk etter dokumenter ved å bruke SearchHandler

                    // TODO: Hent ut relevant info fra dokumentene, og legg dem inn i prompten som en streng.

                    // TODO: Lag prompten som en streng

                    // TODO: Legg til en ny UserChatMessage i listen 'chatMessages' med prompten du laget

                    // TODO: Send til AI-modell ved å bruke 'ChatHandler'
                    // Det er også mulig å strømme resultatet token for token, som gir en bedre brukeropplevelse.

                    // TODO: Append generert svar til assistantMessage.Content, og kall StateHasChanged.
                    // assistantMessage.Content += update;
                    // StateHasChanged();
                }    
            }
            """
        )
    );

    m("[fuchsia]Implementasjon:[/]");
    m("Gå til filen [yellow]Fagdag.Web.Components.Chat.Chat.razor.cs[/] og implementer metoden [yellow]SendMessage[/].");
    AnsiConsole.Write(codePanel);
    PromptNext("\nTrykk [teal]Enter[/] for å gå til neste steg.");
}

// Test webapplikasjon
void TestWebApp()
{
    m("Kjør prosjektet [yellow]fagdag.web[/] for å teste.");
    PromptNext();
}

#endregion

void Exit()
{
    var exit = AnsiConsole.Prompt(
        new TextPrompt<bool>("Ønsker du å avslutte programmet?")
            .AddChoice(true)
            .AddChoice(false)
            .DefaultValue(false)
            .WithConverter(choice => choice ? "y" : "n")
    );
    if (exit) Environment.Exit(0);
    else Start();
}

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

                m("Test av konfigurasjon i appsettings.json :check_mark_button:");
                successful = true;
            }
            catch (ArgumentException ex)
            {
                m($"Testen gikk ikke som forventet!\n[red]{ex.Message}[/]");
                successful = false;
            }
            catch (UriFormatException ex)
            {
                m($"Vennligst sjekk formatet på endepunktene!\n[red]{ex.Message}[/]");
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
                m($"Test av skillsets vellykket! Skillset med navn {skillset.Name} er opprettet. :check_mark_button:");
                successful = true;
            }
            catch (Exception)
            {
                m($"[red]Noe gikk galt under oppretting av skillsets.[/]\n");
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
    var isRecompiled = AnsiConsole.Prompt(
        new TextPrompt<bool>("Har du husket å rekompilere programmet?")
            .AddChoice(true)
            .AddChoice(false)
            .DefaultValue(true)
            .WithConverter(choice => choice ? "y" : "n")
    );
    if (!isRecompiled) return;
    
    var stepZeroSuccess = TestStepZero();
    if (!stepZeroSuccess) return;

    var stepOneSuccess = await TestStepOne();
    if (!stepOneSuccess) return;

    var stepTwoSuccess = await TestStepTwo();
    if (!stepTwoSuccess) return;

    var stepThreeSuccess = await TestStepThree();
    if (!stepThreeSuccess) return;

    PromptNext();
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
