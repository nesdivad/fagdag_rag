using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Fagdag.Utils;
using Fagdag.Embeddings;

var hostBuilder = Host.CreateApplicationBuilder(args);
hostBuilder.Configuration.AddUserSecrets<Program>();

var app = hostBuilder.Build();

string[] choices = [
    "1. Sett opp miljøet", 
    "2. Prosessering av tekst", 
    "3. Lag embeddings", 
    "4. Lagre embeddings i database", 
    "5. [lime]Test hele løsningen![/]"
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
        case 0: Setup.Init(); break;
        default: AnsiConsole.MarkupLineInterpolated($"Du valgte [yellow]{choice}[/]"); break;
    }
}