using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Players;

public readonly record struct PlayerId
{
    public PlayerId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("PlayerId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct NbaTeamId
{
    public NbaTeamId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("NbaTeamId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}

public sealed record Player
{
    public Player(
        PlayerId id,
        string fullName,
        string normalizedName,
        NbaTeamId? currentTeamId,
        IReadOnlyList<string> positions,
        DateOnly? birthDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Positions cannot contain blank values.", nameof(positions));
        }

        Id = id;
        FullName = fullName.Trim();
        NormalizedName = normalizedName;
        CurrentTeamId = currentTeamId;
        Positions = new ReadOnlyCollection<string>(positions.ToArray());
        BirthDate = birthDate;
    }

    public PlayerId Id { get; }

    public string FullName { get; }

    public string NormalizedName { get; }

    public NbaTeamId? CurrentTeamId { get; }

    public IReadOnlyList<string> Positions { get; }

    public DateOnly? BirthDate { get; }
}

public sealed record NbaTeam
{
    public NbaTeam(NbaTeamId id, string name, string abbreviation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(abbreviation);

        Id = id;
        Name = name.Trim();
        Abbreviation = abbreviation.Trim().ToUpperInvariant();
    }

    public NbaTeamId Id { get; }

    public string Name { get; }

    public string Abbreviation { get; }
}

public sealed record ExternalPlayerIdentity
{
    public ExternalPlayerIdentity(
        PlayerId playerId,
        string provider,
        string externalId,
        DateTimeOffset linkedAt,
        bool confirmedByHuman)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        if (!DataSourceName.IsKnown(provider))
        {
            throw new ArgumentException(
                "Provider is not a canonical data source name.",
                nameof(provider));
        }

        if (linkedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("LinkedAt must be UTC.", nameof(linkedAt));
        }

        PlayerId = playerId;
        Provider = provider;
        ExternalId = externalId;
        LinkedAt = linkedAt;
        ConfirmedByHuman = confirmedByHuman;
    }

    public PlayerId PlayerId { get; }

    public string Provider { get; }

    public string ExternalId { get; }

    public DateTimeOffset LinkedAt { get; }

    public bool ConfirmedByHuman { get; }
}
