using Spectre.Console;
using Fagdag.Utils;
using System.Text.Json;

namespace Fagdag.Embeddings;

public static class Setup
{
    public static void Init()
    {
        var file = File.ReadAllText("appsettings.json");
        var appsettings = JsonSerializer.Deserialize<Dictionary<string, string>>(file) 
            ?? throw new NullReferenceException("appsettings is null. Please contact your nearest supervisor...");
        var username = appsettings["Username"];
        var changed = false;

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("Før vi kan starte med kodingen, må vi sette opp noen variabler  :hammer_and_pick:");
        AnsiConsole.MarkupLine("Vi starter med å legge inn noen variabler i [yellow]appsettings.json[/]:\n");

        var fname = AnsiConsole.Ask<string>("Skriv inn fornavnet ditt [yellow](brukes til å lage ditt eget namespace i databasen)[/]:");
        fname += new Random().NextInt64(0, 1_000).ToString();
        
        if (string.IsNullOrEmpty(username))
        {
            appsettings["Username"] = fname;
            changed = true;
        }
        else
        {
            var overwrite = AnsiConsole.Confirm($"Det nåværende brukernavnet ditt er {username} - Ønsker du å overskrive dette?\n[red]Dette kan medføre at du mister endringer som er gjort i databasen![/]\n");
            if (overwrite)
            {
                appsettings["Username"] = fname;
                changed = true;
            }
        }

        if (changed)
        {
            File.WriteAllText("appsettings.json", JsonSerializer.Serialize(appsettings, new JsonSerializerOptions() { WriteIndented = true }));
            AnsiConsole.MarkupLine($"Navnet ditt er registrert: {fname}");
        }

        AnsiConsole.MarkupLine("Trykk en knapp for å gå videre...");
        AnsiConsole.Console.Input.ReadKey(false);
    }
}