using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;

namespace FantasyBasketball.Infrastructure.Providers.BallDontLie;

public sealed class BallDontLieProvider(
    IHttpClientFactory clientFactory,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IPlayerDirectoryProvider, IScheduleProvider
{
    private const string ParserVersion = "balldontlie-v1";
    private static readonly TimeZoneInfo GameDateZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly BallDontLieClient client = new(clientFactory);

    public string Name => DataSourceName.BallDontLie;

    public DataSourceKind Kind => DataSourceKind.Api;

    public async Task<IReadOnlyList<ExternalTeam>> GetTeamsAsync(
        CancellationToken cancellationToken)
    {
        using var document = await client.GetAsync("/v1/teams", cancellationToken);
        return document.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(element => new ExternalTeam(
                ReadId(element),
                element.GetProperty("full_name").GetString()
                    ?? throw MissingValue("full_name"),
                element.GetProperty("abbreviation").GetString()
                    ?? throw MissingValue("abbreviation"),
                CreateProvenance(element, null)))
            .ToArray();
    }

    public async Task<IReadOnlyList<ExternalPlayer>> GetPlayersAsync(
        CancellationToken cancellationToken)
    {
        var elements = await GetAllDataAsync(
            "/v1/players?per_page=100",
            cancellationToken);
        var results = new List<ExternalPlayer>();

        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var teamElement = element.GetProperty("team");
            var team = await ResolveTeamAsync(teamElement, cancellationToken);
            var firstName = element.GetProperty("first_name").GetString()
                ?? throw MissingValue("first_name");
            var lastName = element.GetProperty("last_name").GetString()
                ?? throw MissingValue("last_name");
            var position = element.GetProperty("position").GetString();
            var positions = string.IsNullOrWhiteSpace(position)
                ? []
                : position.Split(
                    '-',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var externalId = ReadId(element);

            results.Add(new ExternalPlayer(
                externalId,
                $"{firstName} {lastName}",
                team.Id,
                positions,
                null,
                CreateProvenance(element, null)));
        }

        return results;
    }

    public async Task<IReadOnlyList<NbaGame>> GetGamesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(to));
        }

        var relativeUri = string.Create(
            CultureInfo.InvariantCulture,
            $"/v1/games?start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&per_page=100");
        var elements = await GetAllDataAsync(relativeUri, cancellationToken);
        var results = new List<NbaGame>();

        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var homeTeam = await ResolveTeamAsync(
                element.GetProperty("home_team"),
                cancellationToken);
            var awayTeam = await ResolveTeamAsync(
                element.GetProperty("visitor_team"),
                cancellationToken);
            var startsAt = ReadStartsAt(element);
            var externalId = ReadId(element);

            results.Add(new NbaGame(
                Guid.NewGuid(),
                element.GetProperty("season").GetInt32() + 1,
                startsAt,
                homeTeam.Id,
                awayTeam.Id,
                ReadNullableInt32(element, "home_team_score"),
                ReadNullableInt32(element, "visitor_team_score"),
                element.GetProperty("status").GetString()
                    ?? throw MissingValue("status"),
                CreateProvenance(element, null)));
        }

        return results;
    }

    private async Task<NbaTeam> ResolveTeamAsync(
        JsonElement element,
        CancellationToken cancellationToken)
    {
        var abbreviation = element.GetProperty("abbreviation").GetString()
            ?? throw MissingValue("team.abbreviation");
        return await teams.FindByAbbreviationAsync(abbreviation, cancellationToken)
            ?? throw new InvalidOperationException(
                $"NBA team '{abbreviation}' must be imported before dependent records.");
    }

    private async Task<IReadOnlyList<JsonElement>> GetAllDataAsync(
        string relativeUri,
        CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        var baseUri = relativeUri;
        string? nextUri = relativeUri;

        while (nextUri is not null)
        {
            using var document = await client.GetAsync(nextUri, cancellationToken);
            results.AddRange(document.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(element => element.Clone()));
            nextUri = ReadNextUri(document.RootElement, baseUri);
        }

        return results;
    }

    private static string? ReadNextUri(JsonElement root, string currentUri)
    {
        if (!root.TryGetProperty("meta", out var meta)
            || !meta.TryGetProperty("next_cursor", out var cursor)
            || cursor.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var cursorValue = cursor.ValueKind == JsonValueKind.String
            ? cursor.GetString()
            : cursor.GetRawText();
        if (string.IsNullOrWhiteSpace(cursorValue))
        {
            return null;
        }

        var separator = currentUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{currentUri}{separator}cursor={Uri.EscapeDataString(cursorValue)}";
    }

    private DataProvenance CreateProvenance(
        JsonElement element,
        DateTimeOffset? sourceTimestamp) =>
        new(
            DataSourceName.BallDontLie,
            ReadId(element),
            timeProvider.GetUtcNow(),
            sourceTimestamp,
            ParserVersion,
            DataSourceConfidence.OfficialApi,
            Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(element.GetRawText())))
                .ToLowerInvariant());

    /// <summary>
    /// balldontlie leaves <c>datetime</c> null on some older games (all 11 on 2022-12-02)
    /// while <c>date</c> is set. Tip-off is then unknown, so use noon US Eastern on that
    /// date: the box-score importer reads the Eastern date back, and midnight UTC would
    /// land on the previous day.
    /// </summary>
    private static DateTimeOffset ReadStartsAt(JsonElement element)
    {
        if (element.TryGetProperty("datetime", out var datetime) && datetime.GetString() is { Length: > 0 } value)
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .ToUniversalTime();
        }

        var date = DateOnly.ParseExact(
            element.TryGetProperty("date", out var dateValue) ? dateValue.GetString() ?? throw MissingValue("datetime")
                : throw MissingValue("datetime"),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);
        var noonEastern = date.ToDateTime(new TimeOnly(12, 0));
        return new DateTimeOffset(noonEastern, GameDateZone.GetUtcOffset(noonEastern)).ToUniversalTime();
    }

    private static string ReadId(JsonElement element) =>
        element.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);

    private static int? ReadNullableInt32(JsonElement element, string propertyName)
    {
        var property = element.GetProperty(propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : property.GetInt32();
    }

    private static InvalidDataException MissingValue(string propertyName) =>
        new($"The balldontlie payload is missing '{propertyName}'.");
}
