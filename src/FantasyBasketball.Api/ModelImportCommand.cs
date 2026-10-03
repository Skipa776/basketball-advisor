using System.Globalization;
using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Modeling;

namespace FantasyBasketball.Api;

/// <summary>
/// Loads a parameter record written by tools/modeling into the model registry
/// (model_params_contract). CLI-only, like the backtest:
/// <c>dotnet run --project src/FantasyBasketball.Api -- models import tools/modeling/out/heat_prior.json [--activate]</c>
/// </summary>
public static class ModelImportCommand
{
    public static async Task<bool> TryRunAsync(WebApplication app, string[] args)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (args is not ["models", "import", var path, ..])
        {
            return false;
        }

        using var record = JsonDocument.Parse(await File.ReadAllTextAsync(RepoPath.Resolve(path)));
        var root = record.RootElement;
        var version = new ModelVersion(
            root.GetProperty("modelName").GetString()!,
            root.GetProperty("version").GetString()!,
            DateTimeOffset.Parse(root.GetProperty("fittedAt").GetString()!, CultureInfo.InvariantCulture),
            root.GetProperty("trainSeasonEndYears").EnumerateArray().Select(year => year.GetInt32()).ToArray(),
            root.GetProperty("parameters").GetRawText(),
            root.GetProperty("metrics").GetRawText(),
            root.GetProperty("cardMarkdown").GetString() ?? string.Empty);
        await using var scope = app.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IModelVersionRepository>();
        await registry.AddAsync(version, CancellationToken.None);
        if (args.Contains("--activate"))
        {
            await registry.ActivateAsync(version.ModelName, version.Version, CancellationToken.None);
        }

        Console.WriteLine($"Imported {version.ModelName} {version.Version}{(args.Contains("--activate") ? " (active)" : string.Empty)}");
        return true;
    }
}
