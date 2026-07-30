using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Infrastructure.Import;

public sealed record ManualAdpInput(
    string ExternalId,
    string PlayerName,
    decimal AverageDraftPosition,
    decimal? StandardDeviation);

public sealed class ManualAdpProvider(
    IReadOnlyList<ManualAdpInput> inputs,
    TimeProvider timeProvider) : IAdpProvider
{
    public string Name => DataSourceName.Manual;

    public DataSourceKind Kind => DataSourceKind.Manual;

    public Task<IReadOnlyList<AdpEntry>> GetAdpAsync(
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AdpEntry> entries = inputs
            .Select(input =>
            {
                var raw = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{input.ExternalId}|{input.PlayerName}|{input.AverageDraftPosition}|{input.StandardDeviation}");
                return new AdpEntry(
                    input.ExternalId,
                    input.PlayerName,
                    input.AverageDraftPosition,
                    input.StandardDeviation,
                    new DataProvenance(
                        DataSourceName.Manual,
                        input.ExternalId,
                        timeProvider.GetUtcNow(),
                        null,
                        "manual-v1",
                        DataSourceConfidence.ManualEntry,
                        Hash(raw)));
            })
            .ToArray();
        return Task.FromResult(entries);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
