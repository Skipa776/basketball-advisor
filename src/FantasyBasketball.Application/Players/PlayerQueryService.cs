using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Players;

public sealed class PlayerQueryService(
    IPlayerRepository players,
    IPlayerQueryRepository queries)
{
    public async Task<Player> GetAsync(
        PlayerId id,
        CancellationToken cancellationToken) =>
        await players.GetAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"Player '{id.Value}' was not found.");

    public Task<PagedResult<Player>> ListAsync(
        string? search,
        string? team,
        string? position,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        Paging.Validate(page, limit);
        return queries.ListAsync(
            search,
            team,
            position,
            page,
            limit,
            cancellationToken);
    }
}

public static class Paging
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 200;

    public static void Validate(int page, int limit)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (limit is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }
}
