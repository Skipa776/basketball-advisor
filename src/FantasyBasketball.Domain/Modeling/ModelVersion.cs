namespace FantasyBasketball.Domain.Modeling;

public sealed record ModelVersion
{
    public const int FirstSeasonEndYear = 1947;

    public ModelVersion(
        string modelName,
        string version,
        DateTimeOffset fittedAt,
        IReadOnlyList<int> trainSeasonEndYears,
        string parametersJson,
        string metricsJson,
        string cardMarkdown)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be blank.", nameof(modelName));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Model version cannot be blank.", nameof(version));
        }

        if (trainSeasonEndYears is null || trainSeasonEndYears.Count == 0)
        {
            throw new ArgumentException(
                "Training seasons cannot be empty.",
                nameof(trainSeasonEndYears));
        }

        foreach (var year in trainSeasonEndYears)
        {
            if (year < FirstSeasonEndYear)
            {
                throw new ArgumentException(
                    $"Season end year {year} predates the {FirstSeasonEndYear} NBA season.",
                    nameof(trainSeasonEndYears));
            }
        }

        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            throw new ArgumentException("Parameters json cannot be blank.", nameof(parametersJson));
        }

        if (string.IsNullOrWhiteSpace(metricsJson))
        {
            throw new ArgumentException("Metrics json cannot be blank.", nameof(metricsJson));
        }

        ModelName = modelName;
        Version = version;
        FittedAt = fittedAt;
        TrainSeasonEndYears = trainSeasonEndYears;
        ParametersJson = parametersJson;
        MetricsJson = metricsJson;
        CardMarkdown = cardMarkdown;
    }

    public string ModelName { get; }

    public string Version { get; }

    public DateTimeOffset FittedAt { get; }

    public IReadOnlyList<int> TrainSeasonEndYears { get; }

    public string ParametersJson { get; }

    public string MetricsJson { get; }

    public string CardMarkdown { get; }
}
