using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace FantasyBasketball.Infrastructure.Http;

public sealed class HostRateLimiter(TimeSpan minimumInterval) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, RateLimiter> limiters =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask WaitAsync(
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        if (!requestUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Request URI must be absolute.", nameof(requestUri));
        }

        var hostKey = requestUri.GetLeftPart(UriPartial.Authority);
        var limiter = limiters.GetOrAdd(hostKey, _ => CreateLimiter());
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                $"The request queue for host '{requestUri.Host}' is full.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var limiter in limiters.Values)
        {
            await limiter.DisposeAsync();
        }

        limiters.Clear();
    }

    private RateLimiter CreateLimiter() =>
        new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 1_000,
            ReplenishmentPeriod = minimumInterval,
            TokensPerPeriod = 1,
            AutoReplenishment = true,
        });
}
