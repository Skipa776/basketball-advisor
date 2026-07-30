using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Abstractions;

public sealed record ExternalPlayer
{
    public ExternalPlayer(
        string externalId,
        string fullName,
        NbaTeamId? teamId,
        IReadOnlyList<string> positions,
        DateOnly? birthDate,
        DataProvenance provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(provenance);

        if (positions.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Positions cannot contain blank values.", nameof(positions));
        }

        ExternalId = externalId;
        FullName = fullName.Trim();
        TeamId = teamId;
        Positions = new ReadOnlyCollection<string>(positions.ToArray());
        BirthDate = birthDate;
        Provenance = provenance;
    }

    public string ExternalId { get; }

    public string FullName { get; }

    public NbaTeamId? TeamId { get; }

    public IReadOnlyList<string> Positions { get; }

    public DateOnly? BirthDate { get; }

    public DataProvenance Provenance { get; }
}
