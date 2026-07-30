using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Context;

namespace FantasyBasketball.Application.Context;

public sealed class ContextEventQueryService(IContextEventQueryRepository events)
{
    public Task<PagedResult<ContextEvent>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        Paging.Validate(page, limit);
        return events.ListAsync(page, limit, cancellationToken);
    }
}
