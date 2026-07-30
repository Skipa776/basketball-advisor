using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;

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

public interface IScheduleProvider : IDataSource
{
    Task<IReadOnlyList<NbaGame>> GetGamesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
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
