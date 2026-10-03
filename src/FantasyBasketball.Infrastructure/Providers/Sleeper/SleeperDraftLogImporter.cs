using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Infrastructure.Providers.Sleeper;

/// <summary>
/// Public completed Sleeper drafts for the opponent pick model, through the allowlisted
/// read-only endpoints and the shared per-host limiter (scraping_policy, owner-authorized 2026-10-02).
/// </summary>
public sealed class SleeperDraftLogImporter(IHttpClientFactory clientFactory) : IDraftLogSource
{
    public string Name => DataSourceName.Sleeper;

    public async Task<IReadOnlyList<string>> SeedUsersAsync(string seed, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);
        if (SleeperUrlBuilder.IsLeagueId(seed))
        {
            using var users = await GetAsync(SleeperUrlBuilder.Users(seed), cancellationToken);
            if (users.RootElement.ValueKind == JsonValueKind.Array)
            {
                return users.RootElement.EnumerateArray().Select(user => user.GetProperty("user_id").GetString()!).ToArray();
            }
        }

        using var user = await GetAsync(SleeperUrlBuilder.User(seed), cancellationToken);
        return user.RootElement.ValueKind == JsonValueKind.Object
            ? [user.RootElement.GetProperty("user_id").GetString()!]
            : throw new KeyNotFoundException("Sleeper has no league or user with that id or name.");
    }

    public async Task<IReadOnlyList<DraftSummary>> ListUserDraftsAsync(string userId, int season, CancellationToken cancellationToken)
    {
        using var drafts = await GetAsync(SleeperUrlBuilder.UserDrafts(userId, season), cancellationToken);
        return drafts.RootElement.ValueKind != JsonValueKind.Array
            ? []
            : drafts.RootElement.EnumerateArray().Select(Summary).ToArray();
    }

    public async Task<DraftLog> GetDraftAsync(DraftSummary draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        using var picks = await GetAsync(SleeperUrlBuilder.DraftPicks(draft.DraftId), cancellationToken);
        var mapped = picks.RootElement.EnumerateArray()
            .Where(pick => !(pick.TryGetProperty("is_keeper", out var keeper) && keeper.ValueKind == JsonValueKind.True))
            .Select(pick =>
            {
                var metadata = pick.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object ? meta : default;
                var position = Text(metadata, "position") ?? "";
                return new DraftLogPick(
                    pick.GetProperty("pick_no").GetInt32(),
                    pick.GetProperty("round").GetInt32(),
                    pick.GetProperty("draft_slot").GetInt32(),
                    pick.GetProperty("player_id").GetString()!,
                    $"{Text(metadata, "first_name")} {Text(metadata, "last_name")}".Trim(),
                    position.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    Text(pick, "picked_by") is { Length: > 0 } by ? by : null);
            })
            .ToArray();
        return new DraftLog(Name, draft.DraftId, draft.Season, draft.TeamCount, draft.Rounds, draft.ScoringType, draft.Slots, draft.StartedAt, mapped);
    }

    private static DraftSummary Summary(JsonElement draft)
    {
        var settings = draft.GetProperty("settings");
        var slots = settings.EnumerateObject()
            .Where(field => field.Name.StartsWith("slots_", StringComparison.Ordinal) && field.Value.ValueKind == JsonValueKind.Number)
            .ToDictionary(field => field.Name["slots_".Length..].ToUpperInvariant(), field => field.Value.GetInt32());
        var metadata = draft.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object ? meta : default;
        return new DraftSummary(
            draft.GetProperty("draft_id").GetString()!,
            int.TryParse(Text(draft, "season"), out var season) ? season : 0,
            Text(draft, "status") ?? "",
            Text(draft, "type") ?? "",
            Number(settings, "teams"),
            Number(settings, "rounds"),
            Text(metadata, "scoring_type"),
            slots,
            draft.TryGetProperty("start_time", out var start) && start.ValueKind == JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeMilliseconds(start.GetInt64())
                : DateTimeOffset.UnixEpoch);
    }

    private static int Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(DataSourceName.Sleeper);
        using var response = await client.GetAsync(SleeperUrlBuilder.Build(path), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Sleeper has nothing at {path}.");
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
