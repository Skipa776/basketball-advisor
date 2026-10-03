using System.Globalization;
using FantasyBasketball.Application.Draft;

namespace FantasyBasketball.Api;

/// <summary>
/// Collects public completed Sleeper drafts for the opponent pick model (scraping_policy,
/// owner-authorized 2026-10-02). CLI-only: a crawl is hundreds of rate-limited requests.
/// <c>dotnet run --project src/FantasyBasketball.Api -- drafts import --seed &lt;league id or username&gt; --season 2025 [--max 300] [--max-users 2000]</c>
/// </summary>
public static class DraftLogCommand
{
    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (args is not ["drafts", "import", ..])
        {
            return false;
        }

        var seed = Option(args, "--seed") ?? throw new ArgumentException("drafts import requires --seed <Sleeper league id or username>.");
        var season = int.Parse(Option(args, "--season") ?? throw new ArgumentException("drafts import requires --season."), CultureInfo.InvariantCulture);
        var maxDrafts = int.Parse(Option(args, "--max") ?? "300", CultureInfo.InvariantCulture);
        var maxUsers = int.Parse(Option(args, "--max-users") ?? "2000", CultureInfo.InvariantCulture);
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<DraftLogImportService>()
            .ImportAsync(seed, season, maxDrafts, maxUsers, CancellationToken.None);
        Console.WriteLine($"Imported {result.Imported} drafts ({result.Skipped} skipped, {result.UsersVisited} users visited); {result.TotalStored} stored for {season}.");
        return true;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
