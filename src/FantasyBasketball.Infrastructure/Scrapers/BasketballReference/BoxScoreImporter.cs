using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Trends;
using Microsoft.Extensions.Logging;

namespace FantasyBasketball.Infrastructure.Scrapers.BasketballReference;

/// <summary>
/// Walks the stored balldontlie schedule and stores one Basketball-Reference box
/// score per final game. Progress is the stored snapshots, so a rerun resumes.
/// </summary>
public sealed class BoxScoreImporter(
    IHttpClientFactory clientFactory,
    IGameRepository games,
    IBoxScoreRepository boxScores,
    ITeamRepository teams,
    PlayerIdentityResolver identityResolver,
    IDataImportRunRepository runs,
    TimeProvider timeProvider,
    ILogger<BoxScoreImporter> logger)
{
    // NBA game dates are US Eastern; balldontlie stores tip-off in UTC.
    private static readonly TimeZoneInfo GameDateZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly BbrefUrlBuilder urlBuilder = new();
    private readonly BoxScoreParser parser = new();

    /// <summary>
    /// The caller asserts the range is regular season: the stored schedule does
    /// not carry balldontlie's postseason flag, and phase is never inferred.
    /// </summary>
    public async Task<DataImportRun> ImportRegularSeasonAsync(
        DateOnly from,
        DateOnly to,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var scheduled = (await games.ListFinalAsync(
                DataSourceName.BallDontLie,
                StartOfDayUtc(from),
                StartOfDayUtc(to.AddDays(1)),
                cancellationToken))
            .Where(game => PlayedOn(game.Game.StartsAt) is var day && day >= from && day <= to)
            .ToArray();
        var written = 0;
        var unresolved = 0;
        var failures = new List<string>();
        for (var index = 0; index < scheduled.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var game = scheduled[index];
            if (await boxScores.ExistsAsync(game.Game.Id, DataSourceName.BasketballReference, cancellationToken))
            {
                continue;
            }

            var playedOn = PlayedOn(game.Game.StartsAt);
            var externalId = BbrefUrlBuilder.CreateBoxScoreGameId(
                playedOn,
                BbrefTeamCodes.FromBallDontLie(game.HomeAbbreviation));
            try
            {
                var result = await ImportGameAsync(game, playedOn, externalId, cancellationToken);
                written += result.Stored ? 1 : 0;
                unresolved += result.Unresolved;
                logger.LogInformation(
                    "Box score {GameId} imported ({Index} of {Total})",
                    externalId,
                    index + 1,
                    scheduled.Length);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(externalId);
                logger.LogWarning(
                    "Box score {GameId} failed with {ExceptionType}; continuing",
                    externalId,
                    exception.GetType().Name);
            }
        }

        var run = new DataImportRun(
            runId ?? Guid.NewGuid(),
            DataSourceName.BasketballReference,
            failures.Count == 0 ? DataImportRunStatus.Succeeded : DataImportRunStatus.Failed,
            startedAt,
            timeProvider.GetUtcNow(),
            written,
            unresolved,
            failures.Count == 0
                ? null
                : $"{failures.Count} of {scheduled.Length} games failed: {string.Join(", ", failures.Take(20))}");
        await runs.AddAsync(run, cancellationToken);
        return run;
    }

    private async Task<(bool Stored, int Unresolved)> ImportGameAsync(
        ScheduledGame game,
        DateOnly playedOn,
        string externalId,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(DataSourceName.BasketballReference);
        var html = await client.GetStringAsync(
            urlBuilder.CreateBoxScoreUri(playedOn, externalId[^3..]),
            cancellationToken);
        var parsed = parser.Parse(html, externalId);
        var provenance = new DataProvenance(
            DataSourceName.BasketballReference,
            externalId,
            timeProvider.GetUtcNow(),
            game.Game.StartsAt,
            BoxScoreParser.ParserVersion,
            0.95m,
            parsed.RawRecordHash);
        var samples = new List<PlayerGameSample>();
        var unresolved = 0;
        foreach (var player in parsed.Players)
        {
            var team = await teams.FindByAbbreviationAsync(
                BbrefTeamCodes.ToBallDontLie(player.TeamCode),
                cancellationToken);
            var resolution = await identityResolver.ResolveAsync(
                new ExternalPlayer(player.ExternalPlayerId, player.PlayerName, team?.Id, [], null, provenance),
                cancellationToken);
            if (resolution.Player is null)
            {
                unresolved++;
                continue;
            }

            samples.Add(new PlayerGameSample(
                game.Game.Id,
                resolution.Player.Id,
                game.Game.SeasonEndYear,
                playedOn,
                true,
                player.DidPlay,
                player.Statistics,
                provenance));
        }

        var stored = await boxScores.AddAsync(
            new CompletedBoxScore(
                game.Game.Id,
                game.Game.SeasonEndYear,
                playedOn,
                NbaGamePhase.RegularSeason,
                provenance,
                samples),
            cancellationToken);
        return (stored, unresolved);
    }

    private static DateOnly PlayedOn(DateTimeOffset startsAt) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startsAt, GameDateZone).DateTime);

    private static DateTimeOffset StartOfDayUtc(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), GameDateZone), TimeSpan.Zero);
}
