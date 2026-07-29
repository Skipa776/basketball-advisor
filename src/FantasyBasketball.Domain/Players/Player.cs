using System.Collections.ObjectModel;

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

public sealed record NbaTeam(NbaTeamId Id, string Name, string Abbreviation);

public sealed record ExternalPlayerIdentity(
    PlayerId PlayerId,
    string Provider,
    string ExternalId,
    DateTimeOffset LinkedAt,
    bool ConfirmedByHuman);
