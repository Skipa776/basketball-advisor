using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Domain.Players;

public sealed record PendingIdentityMatch
{
    public PendingIdentityMatch(
        Guid id,
        string provider,
        string externalId,
        string fullName,
        string normalizedName,
        IReadOnlyList<PlayerId> candidatePlayerIds,
        DateTimeOffset createdAt,
        string reason)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Pending identity match id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);
        ArgumentNullException.ThrowIfNull(candidatePlayerIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!DataSourceName.IsKnown(provider))
        {
            throw new ArgumentException(
                "Provider is not a canonical data source name.",
                nameof(provider));
        }

        if (createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAt must be UTC.", nameof(createdAt));
        }

        var distinctCandidateIds = candidatePlayerIds.Distinct().ToArray();
        if (distinctCandidateIds.Length == 0)
        {
            throw new ArgumentException(
                "A pending match requires at least one candidate player.",
                nameof(candidatePlayerIds));
        }

        Id = id;
        Provider = provider;
        ExternalId = externalId;
        FullName = fullName.Trim();
        NormalizedName = normalizedName;
        CandidatePlayerIds = new ReadOnlyCollection<PlayerId>(distinctCandidateIds);
        CreatedAt = createdAt;
        Reason = reason;
    }

    public Guid Id { get; }

    public string Provider { get; }

    public string ExternalId { get; }

    public string FullName { get; }

    public string NormalizedName { get; }

    public IReadOnlyList<PlayerId> CandidatePlayerIds { get; }

    public DateTimeOffset CreatedAt { get; }

    public string Reason { get; }
}
