using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Ingestion;

public sealed record PlayerIdentityResolution(
    Player? Player,
    PendingIdentityMatch? PendingMatch,
    bool CreatedPlayer);

public sealed class PlayerIdentityResolver(
    IPlayerRepository players,
    TimeProvider timeProvider)
{
    public async Task<PlayerIdentityResolution> ResolveAsync(
        ExternalPlayer externalPlayer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(externalPlayer);

        var provider = externalPlayer.Provenance.Source;
        var knownPlayer = await players.FindByExternalIdentityAsync(
            provider,
            externalPlayer.ExternalId,
            cancellationToken);
        if (knownPlayer is not null)
        {
            return new PlayerIdentityResolution(knownPlayer, null, false);
        }

        var normalizedName = PlayerName.Normalize(externalPlayer.FullName);
        var nameMatches = await players.FindByNormalizedNameAsync(
            normalizedName,
            cancellationToken);
        var candidate = SelectCandidate(nameMatches, externalPlayer);

        if (candidate is not null)
        {
            var providerIdentity = await players.FindIdentityAsync(
                candidate.Id,
                provider,
                cancellationToken);
            if (providerIdentity is not null
                && providerIdentity.ExternalId != externalPlayer.ExternalId)
            {
                return await RecordPendingAsync(
                    externalPlayer,
                    normalizedName,
                    [candidate.Id],
                    "The matched player already has a different id for this provider.",
                    cancellationToken);
            }

            await players.AddResolvedIdentityAsync(
                candidate,
                CreateIdentity(candidate.Id, externalPlayer),
                false,
                cancellationToken);
            return new PlayerIdentityResolution(candidate, null, false);
        }

        if (nameMatches.Count > 0)
        {
            return await RecordPendingAsync(
                externalPlayer,
                normalizedName,
                nameMatches.Select(player => player.Id).ToArray(),
                "More than one canonical player matches the imported identity.",
                cancellationToken);
        }

        var created = new Player(
            new PlayerId(Guid.NewGuid()),
            externalPlayer.FullName,
            normalizedName,
            externalPlayer.TeamId,
            externalPlayer.Positions,
            externalPlayer.BirthDate);
        await players.AddResolvedIdentityAsync(
            created,
            CreateIdentity(created.Id, externalPlayer),
            true,
            cancellationToken);
        return new PlayerIdentityResolution(created, null, true);
    }

    private static Player? SelectCandidate(
        IReadOnlyList<Player> nameMatches,
        ExternalPlayer externalPlayer)
    {
        if (externalPlayer.TeamId is { } teamId)
        {
            var teamMatches = nameMatches
                .Where(player => player.CurrentTeamId == teamId)
                .ToArray();
            if (teamMatches.Length == 1)
            {
                return teamMatches[0];
            }
        }

        if (nameMatches.Count == 1)
        {
            return nameMatches[0];
        }

        if (externalPlayer.BirthDate is { } birthDate)
        {
            var birthDateMatches = nameMatches
                .Where(player => player.BirthDate == birthDate)
                .ToArray();
            if (birthDateMatches.Length == 1)
            {
                return birthDateMatches[0];
            }
        }

        return null;
    }

    private ExternalPlayerIdentity CreateIdentity(
        PlayerId playerId,
        ExternalPlayer externalPlayer) =>
        new(
            playerId,
            externalPlayer.Provenance.Source,
            externalPlayer.ExternalId,
            timeProvider.GetUtcNow(),
            false);

    private async Task<PlayerIdentityResolution> RecordPendingAsync(
        ExternalPlayer externalPlayer,
        string normalizedName,
        IReadOnlyList<PlayerId> candidatePlayerIds,
        string reason,
        CancellationToken cancellationToken)
    {
        var pending = new PendingIdentityMatch(
            Guid.NewGuid(),
            externalPlayer.Provenance.Source,
            externalPlayer.ExternalId,
            externalPlayer.FullName,
            normalizedName,
            candidatePlayerIds,
            timeProvider.GetUtcNow(),
            reason);
        await players.AddPendingIdentityMatchAsync(pending, cancellationToken);
        return new PlayerIdentityResolution(null, pending, false);
    }
}
