using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace FantasyBasketball.Infrastructure.Http;

public sealed class ResponseCacheHandler(
    IMemoryCache cache,
    TimeSpan freshnessWindow) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var cacheKey = $"{request.Method.Method}:{request.RequestUri.AbsoluteUri}";
        if (cache.TryGetValue<CachedResponse>(cacheKey, out var cached)
            && cached is not null)
        {
            return cached.CreateResponse(request);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return response;
        }

        var cachedResponse = await CachedResponse.CreateAsync(
            response,
            cancellationToken);
        response.Dispose();
        cache.Set(cacheKey, cachedResponse, freshnessWindow);
        return cachedResponse.CreateResponse(request);
    }

    private sealed record CachedResponse(
        HttpStatusCode StatusCode,
        Version Version,
        string? ReasonPhrase,
        byte[] Content,
        IReadOnlyDictionary<string, string[]> Headers,
        IReadOnlyDictionary<string, string[]> ContentHeaders)
    {
        public static async Task<CachedResponse> CreateAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            var content = response.Content is null
                ? []
                : await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return new CachedResponse(
                response.StatusCode,
                response.Version,
                response.ReasonPhrase,
                content,
                response.Headers.ToDictionary(
                    header => header.Key,
                    header => header.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
                response.Content?.Headers.ToDictionary(
                    header => header.Key,
                    header => header.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
        }

        public HttpResponseMessage CreateResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(StatusCode)
            {
                Version = Version,
                ReasonPhrase = ReasonPhrase,
                RequestMessage = request,
                Content = new ByteArrayContent(Content),
            };

            foreach (var header in Headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var header in ContentHeaders)
            {
                response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        }
    }
}
