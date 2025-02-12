namespace Fagdag.Utils;

public static class TangOgTare
{
    public static DirectoryInfo? GetSolutionDir(string? currentPath = null)
    {
        var directory = new DirectoryInfo(currentPath ?? Directory.GetCurrentDirectory());
        while (directory is not null && directory.GetFiles("*.sln").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory;
    }

    public static string GetOrCreateUsername()
    {
        var filename = "user.txt";
        var solutionDirectory = GetSolutionDir(Directory.GetCurrentDirectory());
        var path = solutionDirectory is null 
            ? Path.Combine(Directory.GetCurrentDirectory(), filename) 
            : Path.Combine(solutionDirectory.FullName, filename);
            
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }
        else
        {
            var rand = new Random();
            var user = $"u{rand.Next(0, 1_000_000)}";
            File.WriteAllText(path, user);
            return user;
        }
    }
}

public delegate void AnsiMarkup(string markup);