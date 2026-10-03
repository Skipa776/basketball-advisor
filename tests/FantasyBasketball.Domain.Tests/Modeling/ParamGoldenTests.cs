using System.Text.Json;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Modeling;

/// <summary>
/// model_params_contract: C# recomputes every Python golden from the same parameters.
/// A golden for a model with no evaluator here fails, so a new model cannot skip the check.
/// </summary>
public sealed class ParamGoldenTests
{
    private const decimal Tolerance = 0.000001m;

    [Fact]
    public void MG01_csharp_matches_every_python_golden()
    {
        var goldens = Directory.GetFiles(Path.Combine(RepoRoot(), "tools", "modeling", "goldens"), "*.json");
        goldens.ShouldNotBeEmpty();
        foreach (var path in goldens)
        {
            using var golden = JsonDocument.Parse(File.ReadAllText(path));
            var model = golden.RootElement.GetProperty("model").GetString();
            switch (model)
            {
                case HeatPriorParameters.ModelName:
                    CheckHeatPrior(golden.RootElement);
                    break;
                case ProjectionRateParameters.ModelName:
                    CheckProjectionRates(golden.RootElement);
                    break;
                case MinutesModelParameters.ModelName:
                    CheckMinutes(golden.RootElement);
                    break;
                case AvailabilityModelParameters.ModelName:
                    CheckAvailability(golden.RootElement);
                    break;
                default:
                    throw new ShouldAssertException($"No C# evaluator for golden model '{model}' ({path}).");
            }
        }
    }

    private static void CheckHeatPrior(JsonElement golden)
    {
        var classifier = new HeatClassifier(HeatPriorParameters.Parse(golden.GetProperty("parameters").GetRawText()));
        foreach (var testCase in golden.GetProperty("cases").EnumerateArray())
        {
            var actual = classifier.Posterior(
                Values(testCase.GetProperty("baseline")),
                testCase.GetProperty("baselineMinutes").GetDecimal(),
                Values(testCase.GetProperty("recent")));
            var expected = testCase.GetProperty("expected");
            Math.Abs(actual.ShiftMean - expected.GetProperty("shiftMean").GetDecimal()).ShouldBeLessThan(Tolerance);
            Math.Abs(actual.ShiftSd - expected.GetProperty("shiftSd").GetDecimal()).ShouldBeLessThan(Tolerance);
            Math.Abs(actual.EffectFloor - expected.GetProperty("effectFloor").GetDecimal()).ShouldBeLessThan(Tolerance);
            Math.Abs(actual.PHot - expected.GetProperty("pHot").GetDecimal()).ShouldBeLessThan(Tolerance);
            Math.Abs(actual.PCold - expected.GetProperty("pCold").GetDecimal()).ShouldBeLessThan(Tolerance);
        }
    }

    private static void CheckProjectionRates(JsonElement golden)
    {
        var projector = new HierarchicalProjector(ProjectionRateParameters.Parse(golden.GetProperty("parameters").GetRawText()));
        foreach (var testCase in golden.GetProperty("cases").EnumerateArray())
        {
            var history = testCase.GetProperty("history").EnumerateArray()
                .Select(line => new SeasonHistory(
                    line.GetProperty("lag").GetInt32(),
                    new StatLine(line.EnumerateObject()
                        .Where(field => Enum.TryParse<StatKey>(field.Name, out _))
                        .ToDictionary(field => Enum.Parse<StatKey>(field.Name), field => field.Value.GetDecimal())),
                    null))
                .ToArray();
            var age = testCase.GetProperty("age") is { ValueKind: JsonValueKind.Number } value ? value.GetInt32() : (int?)null;
            var group = testCase.GetProperty("group").GetString()!;
            foreach (var expected in testCase.GetProperty("expected").EnumerateObject())
            {
                var actual = projector.Rate(Enum.Parse<StatKey>(expected.Name), group, age, history);
                Math.Abs(actual - expected.Value.GetDecimal()).ShouldBeLessThan(Tolerance);
            }
        }
    }

    private static void CheckMinutes(JsonElement golden)
    {
        var model = new MinutesModel(MinutesModelParameters.Parse(golden.GetProperty("parameters").GetRawText()));
        foreach (var testCase in golden.GetProperty("cases").EnumerateArray())
        {
            var history = testCase.GetProperty("history").EnumerateArray()
                .Select(line => new MinutesHistory(
                    line.GetProperty("lag").GetInt32(),
                    line.GetProperty("games").GetInt32(),
                    line.GetProperty("mpg").GetDecimal(),
                    null))
                .ToArray();
            var age = testCase.GetProperty("age") is { ValueKind: JsonValueKind.Number } value ? value.GetInt32() : (int?)null;
            Math.Abs(model.FromHistory(age, history) - testCase.GetProperty("expected").GetDecimal()).ShouldBeLessThan(Tolerance);
        }
    }

    private static void CheckAvailability(JsonElement golden)
    {
        var model = new AvailabilityModel(AvailabilityModelParameters.Parse(golden.GetProperty("parameters").GetRawText()));
        foreach (var testCase in golden.GetProperty("cases").EnumerateArray())
        {
            var history = testCase.GetProperty("history").EnumerateArray()
                .Select(line => new AvailabilityHistory(
                    line.GetProperty("lag").GetInt32(),
                    line.GetProperty("games").GetInt32(),
                    line.GetProperty("seasonGames").GetInt32(),
                    line.GetProperty("mpg").GetDecimal(),
                    null))
                .ToArray();
            var age = testCase.GetProperty("age") is { ValueKind: JsonValueKind.Number } value ? value.GetInt32() : (int?)null;
            var games = model.FromHistory(age, history);
            var expected = testCase.GetProperty("expected");
            Math.Abs(games.Alpha - expected.GetProperty("alpha").GetDecimal()).ShouldBeLessThan(Tolerance);
            Math.Abs(games.Beta - expected.GetProperty("beta").GetDecimal()).ShouldBeLessThan(Tolerance);
        }
    }

    private static decimal[] Values(JsonElement array) => array.EnumerateArray().Select(value => value.GetDecimal()).ToArray();

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FantasyBasketball.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
