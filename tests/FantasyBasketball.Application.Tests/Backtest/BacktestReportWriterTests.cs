using FantasyBasketball.Application.Backtest;
using FantasyBasketball.Domain.Backtest;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Backtest;

public sealed class BacktestReportWriterTests
{
    [Fact]
    public void BR04_report_renders_metrics_and_deciles_deterministically()
    {
        var model = AccuracyMetrics.Compute([10m, 20m, 30m], [12m, 18m, 30m]);
        var naive = AccuracyMetrics.Compute([11m, 20m, 29m], [12m, 18m, 30m]);
        var result = new ProjectionBacktestResult(
            2026, 2025, new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero), 3, "baseline-v1", model, naive);

        var report = BacktestReportWriter.Render(result, "abc1234");

        report.ShouldBe(BacktestReportWriter.Render(result, "abc1234"));
        report.ShouldStartWith("# Projection backtest — 2025–26 holdout\n");
        report.ShouldContain("As of 2025-10-01 · trained on 2024–25 · 3 players with 20+ games · model `baseline-v1` · commit `abc1234`");
        // Model MAE (2 + 2 + 0) / 3 = 1.3333; naive MAE (1 + 2 + 1) / 3 = 1.3333; delta 0.
        report.ShouldContain("| MAE | 1.3333 | 1.3333 | 0.0000 |");
        // Three players fill deciles 4, 7 and 10 (evenly spread groups skip empty ones).
        report.ShouldContain("| 4 | 1 | 10.0000 | 12.0000 | 2.0000 |");
        report.ShouldNotContain("\r");
    }
}
