namespace FantasyBasketball.Infrastructure.Http;

public sealed class HostRateLimitHandler(HostRateLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestUri = request.RequestUri
            ?? throw new InvalidOperationException("An outbound request requires a URI.");
        await limiter.WaitAsync(requestUri, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
