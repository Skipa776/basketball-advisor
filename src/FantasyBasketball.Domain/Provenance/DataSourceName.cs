namespace FantasyBasketball.Domain.Provenance;

public static class DataSourceName
{
    public const string BallDontLie = "balldontlie";
    public const string BasketballReference = "basketball-reference";
    public const string FantasyPros = "fantasypros";
    public const string Csv = "csv";
    public const string Manual = "manual";

    private static readonly HashSet<string> Values =
        new(StringComparer.Ordinal)
        {
            BallDontLie,
            BasketballReference,
            FantasyPros,
            Csv,
            Manual,
        };

    public static bool IsKnown(string value) => Values.Contains(value);
}

public static class DataSourceConfidence
{
    public const decimal OfficialApi = 0.95m;
    public const decimal HtmlScraper = 0.85m;
    public const decimal CsvImport = 0.80m;
    public const decimal ManualEntry = 1.00m;
}
