using System.Globalization;
using System.Text;
using FantasyBasketball.Domain.Backtest;

namespace FantasyBasketball.Application.Backtest;

/// <summary>Renders a projection backtest as the committed markdown report.</summary>
public static class BacktestReportWriter
{
    public static string Render(ProjectionBacktestResult result, string commit)
    {
        ArgumentNullException.ThrowIfNull(result);
        var report = new StringBuilder();
        Line(report, $"# Projection backtest — {result.EvalSeasonEndYear - 1}–{result.EvalSeasonEndYear % 100:00} holdout");
        Line(report);
        Line(report, $"As of {result.AsOf:yyyy-MM-dd} · trained on {result.TrainSeasonEndYear - 1}–{result.TrainSeasonEndYear % 100:00} · " +
            $"{result.PlayerCount} players with {ProjectionBacktestRunner.MinEvalGames}+ games · model `{result.ModelVersion}` · commit `{commit}`");
        Line(report);
        Line(report, "Fantasy points per game under ESPN default points scoring.");
        Line(report);
        Line(report, "| Metric | Model | Last season repeats | Model − baseline |");
        Line(report, "|---|---|---|---|");
        MetricRow(report, "MAE", result.Model.Mae, result.Naive.Mae);
        MetricRow(report, "RMSE", result.Model.Rmse, result.Naive.Rmse);
        MetricRow(report, "Spearman ρ", result.Model.SpearmanRho, result.Naive.SpearmanRho);
        MetricRow(report, "Top-100 hit rate", result.Model.TopKHitRate, result.Naive.TopKHitRate);
        Line(report);
        Line(report, "## Calibration by decile (model)");
        Line(report);
        Deciles(report, result.Model);
        if (result.Hierarchical is { } hierarchical)
        {
            Line(report);
            Line(report, $"## Hierarchical rates (`{result.HierarchicalVersion}`)");
            Line(report);
            Line(report, result.MinutesVersion is { } minutes
                ? $"Same players as the model above; per-minute rates from the hierarchical model, minutes from `{minutes}`."
                : "Same players and projected minutes as the model above; only the per-minute rates differ.");
            Line(report);
            Line(report, $"| Metric | Hierarchical | {result.ModelVersion} | Last season repeats | Hierarchical − {result.ModelVersion} |");
            Line(report, "|---|---|---|---|---|");
            HierarchicalRow(report, "MAE", hierarchical.Mae, result.Model.Mae, result.Naive.Mae);
            HierarchicalRow(report, "RMSE", hierarchical.Rmse, result.Model.Rmse, result.Naive.Rmse);
            HierarchicalRow(report, "Spearman ρ", hierarchical.SpearmanRho, result.Model.SpearmanRho, result.Naive.SpearmanRho);
            HierarchicalRow(report, "Top-100 hit rate", hierarchical.TopKHitRate, result.Model.TopKHitRate, result.Naive.TopKHitRate);
            if (result.Intervals is { } intervals)
            {
                Line(report);
                Line(report, $"80% intervals held the actual points per game for {Number(intervals.Coverage80)} of {intervals.Count} players " +
                    $"(mean SD {Number(intervals.MeanSd)}; target 0.76–0.84). Distribution models: {result.DistributionVersions}.");
            }

            Line(report);
            Line(report, "### Calibration by decile (hierarchical)");
            Line(report);
            Deciles(report, hierarchical);
        }

        return report.ToString();
    }

    private static void Deciles(StringBuilder report, AccuracyReport accuracy)
    {
        Line(report, "| Decile | Players | Mean projected | Mean actual | Actual − projected |");
        Line(report, "|---|---|---|---|---|");
        foreach (var decile in accuracy.Deciles)
        {
            Line(report, $"| {decile.Decile} | {decile.Count} | {Number(decile.MeanProjected)} | " +
                $"{Number(decile.MeanActual)} | {Number(decile.Deviation)} |");
        }
    }

    private static void HierarchicalRow(StringBuilder report, string name, decimal hierarchical, decimal model, decimal naive) =>
        Line(report, $"| {name} | {Number(hierarchical)} | {Number(model)} | {Number(naive)} | {Number(hierarchical - model)} |");

    private static void MetricRow(StringBuilder report, string name, decimal model, decimal naive) =>
        Line(report, $"| {name} | {Number(model)} | {Number(naive)} | {Number(model - naive)} |");

    private static string Number(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero).ToString("0.0000", CultureInfo.InvariantCulture);

    private static void Line(StringBuilder report, string text = "") => report.Append(text).Append('\n');
}
