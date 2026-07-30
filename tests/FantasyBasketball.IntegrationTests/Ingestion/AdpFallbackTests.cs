using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Import;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Ingestion;

public sealed class AdpFallbackTests
{
    private static readonly DateTimeOffset ImportedAt =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task I08_csv_produces_adp_with_scraper_disabled()
    {
        const string csv =
            "external_id,player_name,adp,standard_deviation\n"
            + "jokic,Nikola Jokic,2.3,1.1\n";
        var importer = new CsvAdpImporter(
            csv,
            new FixedTimeProvider(ImportedAt));

        var entries = await importer.GetAdpAsync(
            TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(1);
        entries[0].AverageDraftPosition.ShouldBe(2.3m);
        entries[0].Provenance.Source.ShouldBe(DataSourceName.Csv);
        entries[0].Provenance.Confidence.ShouldBe(DataSourceConfidence.CsvImport);
    }

    [Fact]
    public async Task I08_manual_produces_adp_with_scraper_disabled()
    {
        var provider = new ManualAdpProvider(
            [
                new ManualAdpInput(
                    "jokic",
                    "Nikola Jokic",
                    2.3m,
                    1.1m),
            ],
            new FixedTimeProvider(ImportedAt));

        var entries = await provider.GetAdpAsync(
            TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(1);
        entries[0].AverageDraftPosition.ShouldBe(2.3m);
        entries[0].Provenance.Source.ShouldBe(DataSourceName.Manual);
        entries[0].Provenance.Confidence.ShouldBe(DataSourceConfidence.ManualEntry);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
