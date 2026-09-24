using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;
using Microsoft.Extensions.Caching.Memory;

namespace FantasyBasketball.Infrastructure.Providers.Sleeper;

/// <summary>
/// Read-only Sleeper NBA league adapter. Each endpoint was validated against a real NBA league
/// (2026-09-24) and is covered by a committed, anonymized fixture. League data is cached briefly
/// by the HTTP client; the ~2.5 MB player map is cached for a day, as Sleeper asks.
/// </summary>
public sealed class SleeperLeagueProvider(IHttpClientFactory clientFactory, IMemoryCache cache, TimeProvider clock)
    : IFantasyLeagueProvider
{
    public const string ParserVersion = "sleeper-v1";
    private static readonly HashSet<string> BasePositions = new(StringComparer.Ordinal) { "PG", "SG", "SF", "PF", "C" };

    public string Name => DataSourceName.Sleeper;

    public DataSourceKind Kind => DataSourceKind.Api;

    public async Task<ExternalLeagueSnapshot> GetLeagueAsync(string externalLeagueId, CancellationToken cancellationToken)
    {
        if (!SleeperUrlBuilder.IsLeagueId(externalLeagueId))
        {
            throw new ArgumentException("A Sleeper league id is the number in sleeper.com/leagues/<id>.", nameof(externalLeagueId));
        }

        using var league = await GetAsync(SleeperUrlBuilder.League(externalLeagueId), cancellationToken);
        var root = league.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            // Sleeper answers an unknown league id with a literal null.
            throw new KeyNotFoundException("Sleeper has no league with that id.");
        }

        if (root.GetProperty("sport").GetString() != "nba")
        {
            throw new InvalidDataException("That Sleeper league is not an NBA league.");
        }

        using var users = await GetAsync(SleeperUrlBuilder.Users(externalLeagueId), cancellationToken);
        using var rosters = await GetAsync(SleeperUrlBuilder.Rosters(externalLeagueId), cancellationToken);
        var players = await PlayersAsync(cancellationToken);
        var fetchedAt = clock.GetUtcNow();

        var owners = users.RootElement.EnumerateArray().ToDictionary(
            user => user.GetProperty("user_id").GetString()!,
            user => (Display: user.TryGetProperty("display_name", out var display) ? display.GetString() : null,
                Team: user.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object
                    && meta.TryGetProperty("team_name", out var team) ? team.GetString() : null));

        var teams = new List<ExternalLeagueTeam>();
        foreach (var roster in rosters.RootElement.EnumerateArray().OrderBy(roster => roster.GetProperty("roster_id").GetInt32()))
        {
            var rosterId = roster.GetProperty("roster_id").GetInt32().ToString(CultureInfo.InvariantCulture);
            var ownerId = roster.TryGetProperty("owner_id", out var owner) && owner.ValueKind == JsonValueKind.String ? owner.GetString() : null;
            var (display, teamName) = ownerId is not null && owners.TryGetValue(ownerId, out var found) ? found : (null, null);
            var rostered = roster.TryGetProperty("players", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(id => id.GetString()!).Where(players.ContainsKey).Select(id => Player(id, players[id], fetchedAt)).ToArray()
                : [];
            teams.Add(new ExternalLeagueTeam(rosterId,
                string.IsNullOrWhiteSpace(teamName) ? display ?? $"Team {rosterId}" : teamName!, display, rostered));
        }

        return new ExternalLeagueSnapshot(DataSourceName.Sleeper, externalLeagueId,
            root.GetProperty("name").GetString() ?? "Sleeper league",
            root.GetProperty("total_rosters").GetInt32(),
            root.GetProperty("roster_positions").EnumerateArray().Select(slot => slot.GetString()!).ToArray(),
            root.GetProperty("scoring_settings").EnumerateObject().ToDictionary(entry => entry.Name, entry => entry.Value.GetDecimal()),
            teams);
    }

    private static ExternalRosterPlayer Player(string id, string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var player = document.RootElement;
        string? Text(string name) => player.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var name = Text("full_name") ?? $"{Text("first_name")} {Text("last_name")}".Trim();
        var positions = player.TryGetProperty("fantasy_positions", out var eligible) && eligible.ValueKind == JsonValueKind.Array
            ? eligible.EnumerateArray().Select(value => value.GetString()!).Where(BasePositions.Contains).Distinct().ToArray()
            : [];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        return new ExternalRosterPlayer(id, name, Text("team"), positions,
            new DataProvenance(DataSourceName.Sleeper, id, fetchedAt, null, ParserVersion, DataSourceConfidence.OfficialApi, hash));
    }

    // Raw JSON per player id; parsed per rostered player only.
    private async Task<IReadOnlyDictionary<string, string>> PlayersAsync(CancellationToken cancellationToken) =>
        (await cache.GetOrCreateAsync("sleeper:players:nba", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            using var document = await GetAsync(SleeperUrlBuilder.Players, cancellationToken);
            return (IReadOnlyDictionary<string, string>)document.RootElement.EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetRawText());
        }))!;

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(DataSourceName.Sleeper);
        using var response = await client.GetAsync(SleeperUrlBuilder.Build(path), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("Sleeper has no league with that id.");
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
