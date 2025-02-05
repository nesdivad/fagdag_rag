using Spectre.Console;
using System.Text.Json;

namespace Fagdag.Embeddings;

public static class Setup
{
    public static void Init()
    {
        var file = File.ReadAllText("appsettings.json");
        Console.WriteLine(file);
        var appsettings = JsonSerializer.Deserialize<Dictionary<string, string>>(file)
            ?? throw new NullReferenceException("appsettings is null. Please contact your nearest supervisor...");

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("Før vi kan starte med kodingen, må vi sette opp noen variabler  :hammer_and_pick:");
        AnsiConsole.MarkupLine("Vi starter med å legge inn noen variabler i [yellow]appsettings.json[/]:\n");

        var fname = AnsiConsole.Ask<string>("Skriv inn fornavnet ditt [yellow](brukes til å lage ditt eget namespace i databasen)[/]: ");
        fname += new Random().NextInt64(0, 1_000).ToString();
        appsettings["Username"] = fname;

        var openaiApikey = AnsiConsole.Ask<string>("Lim inn API-nøkkelen til OpenAI: ");
        appsettings["AZURE_OPENAI_API_KEY"] = openaiApikey;

        var pineconeApiKey = AnsiConsole.Ask<string>("Lim inn API-nøkkelen til Pinecone: ");
        appsettings["PINECONE_API_KEY"] = pineconeApiKey;

        var storageConn = AnsiConsole.Ask<string>("Lim inn connection string for Azure Storage Account: ");
        appsettings["AZURE_STORAGE_ACCOUNT"] = storageConn;

        File.WriteAllText("appsettings.json", JsonSerializer.Serialize(appsettings, new JsonSerializerOptions() { WriteIndented = true }));

        AnsiConsole.MarkupLine("\n[green]Innstillinger lagret![/]");
        AnsiConsole.MarkupLine("Trykk en knapp for å gå videre...->");
        AnsiConsole.Console.Input.ReadKey(false);
    }
}
