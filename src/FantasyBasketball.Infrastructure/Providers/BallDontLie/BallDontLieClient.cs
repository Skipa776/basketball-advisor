using System.Text.Json;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Infrastructure.Providers.BallDontLie;

public sealed class BallDontLieClient(IHttpClientFactory clientFactory)
{
    public async Task<JsonDocument> GetAsync(
        string relativeUri,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(DataSourceName.BallDontLie);
        using var response = await client.GetAsync(relativeUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
    }
}
