namespace FantasyBasketball.Api;

/// <summary>
/// CLI commands take repo-relative paths, but <c>dotnet run</c> starts in the project
/// directory; resolve against the folder holding the solution instead.
/// </summary>
public static class RepoPath
{
    public static string Resolve(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FantasyBasketball.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), path);
    }
}
