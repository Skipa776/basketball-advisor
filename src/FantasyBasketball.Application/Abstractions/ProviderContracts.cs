using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Abstractions;

public enum DataSourceKind
{
    Api,
    Scraper,
    File,
    Manual,
}

public interface IDataSource
{
    string Name { get; }

    DataSourceKind Kind { get; }
}

public interface IPlayerDirectoryProvider : IDataSource
{
    Task<IReadOnlyList<ExternalPlayer>> GetPlayersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalTeam>> GetTeamsAsync(
        CancellationToken cancellationToken);
}

public interface IPlayerStatsProvider : IDataSource
{
    Task<IReadOnlyList<SeasonStatLine>> GetSeasonStatsAsync(
        int seasonEndYear,
        CancellationToken cancellationToken);
}

public interface IAdpProvider : IDataSource
{
    Task<IReadOnlyList<AdpEntry>> GetAdpAsync(
        CancellationToken cancellationToken);
}

public interface IScheduleProvider : IDataSource
{
    Task<IReadOnlyList<NbaGame>> GetGamesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

public sealed record AdpEntry
{
    public AdpEntry(
        string externalId,
        string playerName,
        decimal averageDraftPosition,
        decimal? standardDeviation,
        DataProvenance provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        ArgumentNullException.ThrowIfNull(provenance);

        if (averageDraftPosition <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(averageDraftPosition));
        }

        if (standardDeviation < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardDeviation));
        }

        ExternalId = externalId.Trim();
        PlayerName = playerName.Trim();
        AverageDraftPosition = averageDraftPosition;
        StandardDeviation = standardDeviation;
        Provenance = provenance;
    }

    public string ExternalId { get; }

    public string PlayerName { get; }

    public decimal AverageDraftPosition { get; }

    public decimal? StandardDeviation { get; }

    public DataProvenance Provenance { get; }
}

public sealed record ExternalTeam
{
    public ExternalTeam(
        string externalId,
        string name,
        string abbreviation,
        DataProvenance provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(abbreviation);
        ArgumentNullException.ThrowIfNull(provenance);

        ExternalId = externalId;
        Name = name.Trim();
        Abbreviation = abbreviation.Trim().ToUpperInvariant();
        Provenance = provenance;
    }

    public string ExternalId { get; }

    public string Name { get; }

    public string Abbreviation { get; }

    public DataProvenance Provenance { get; }
}
