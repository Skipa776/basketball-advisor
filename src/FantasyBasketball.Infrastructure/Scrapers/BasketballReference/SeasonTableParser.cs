using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Infrastructure.Scrapers.BasketballReference;

public sealed record ParsedSeasonStatLine(
    string ExternalPlayerId,
    string PlayerName,
    int GamesPlayed,
    decimal MinutesPerGame,
    StatLine PerGame,
    StatLine Totals,
    decimal? UsageRate,
    string RawFragment);

public sealed partial class SeasonTableParser
{
    private const string LeagueAverageRow = "League Average";

    private const string PlayerColumn = "Player";
    private const string TeamColumn = "Team";
    private const decimal PerGameRounding = 0.1m;
    private const string GamesColumn = "G";
    private const string UsageColumn = "USG%";

    public IReadOnlyList<ParsedSeasonStatLine> Parse(
        string perGameHtml,
        string totalsHtml,
        string advancedHtml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(perGameHtml);
        ArgumentException.ThrowIfNullOrWhiteSpace(totalsHtml);
        ArgumentException.ThrowIfNullOrWhiteSpace(advancedHtml);

        var requiredSeasonColumns = new[]
        {
            PlayerColumn,
            GamesColumn,
        }.Concat(StatSourceColumnMap.SeasonTableCounting.Keys).Distinct().ToArray();
        var perGame = ParseTable(
            perGameHtml,
            "per_game_stats",
            requiredSeasonColumns);
        var totals = ParseTable(
            totalsHtml,
            "totals_stats",
            requiredSeasonColumns);
        var advanced = ParseTable(
            advancedHtml,
            "advanced",
            [PlayerColumn, UsageColumn]);
        var totalsByPlayer = totals.ToDictionary(row => row.ExternalPlayerId);
        var advancedByPlayer = advanced.ToDictionary(row => row.ExternalPlayerId);
        var results = new List<ParsedSeasonStatLine>();

        foreach (var perGameRow in perGame)
        {
            if (!totalsByPlayer.TryGetValue(perGameRow.ExternalPlayerId, out var totalsRow))
            {
                throw new InvalidDataException(
                    $"Totals table is missing player '{perGameRow.ExternalPlayerId}'.");
            }

            if (!advancedByPlayer.TryGetValue(perGameRow.ExternalPlayerId, out var advancedRow))
            {
                throw new InvalidDataException(
                    $"Advanced table is missing player '{perGameRow.ExternalPlayerId}'.");
            }

            var perGameStats = CreateStatLine(perGameRow);
            var totalStats = CreateStatLine(totalsRow);
            // Per-game values are each rounded to 0.1, so they can disagree by exactly 0.1;
            // whole-number totals must match exactly.
            EnsureReboundIdentity(perGameStats, perGameRow.ExternalPlayerId, PerGameRounding);
            EnsureReboundIdentity(totalStats, perGameRow.ExternalPlayerId, 0m);
            var usageRate = ParseDecimal(advancedRow, UsageColumn) / 100m;

            results.Add(new ParsedSeasonStatLine(
                perGameRow.ExternalPlayerId,
                perGameRow.PlayerName,
                ParseInt32(perGameRow, GamesColumn),
                perGameStats[StatKey.MIN],
                perGameStats,
                totalStats,
                usageRate,
                string.Concat(
                    perGameRow.RawFragment,
                    totalsRow.RawFragment,
                    advancedRow.RawFragment)));
        }

        return new ReadOnlyCollection<ParsedSeasonStatLine>(results);
    }

    private static IReadOnlyList<ParsedRow> ParseTable(
        string html,
        string tableId,
        IReadOnlyList<string> requiredColumns)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var table = document.QuerySelector($"table#{tableId}");

        if (table is null)
        {
            foreach (var comment in Descendants(document).OfType<IComment>())
            {
                var commentDocument = parser.ParseDocument(comment.Data);
                table = commentDocument.QuerySelector($"table#{tableId}");
                if (table is not null)
                {
                    break;
                }
            }
        }

        if (table is null)
        {
            throw new InvalidDataException($"Missing expected table '{tableId}'.");
        }

        var headerCells = table.QuerySelectorAll("thead tr").LastOrDefault()?
            .Children
            .Select(cell => cell.TextContent.Trim())
            .ToArray()
            ?? [];
        var columnIndexes = headerCells
            .Select((header, index) => (header, index))
            .Where(item => !string.IsNullOrEmpty(item.header))
            .ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);

        foreach (var requiredColumn in requiredColumns)
        {
            if (!columnIndexes.ContainsKey(requiredColumn))
            {
                throw new InvalidDataException(
                    $"Missing expected column '{requiredColumn}' in table '{tableId}'.");
            }
        }

        var rows = new List<ParsedRow>();
        foreach (var row in table.QuerySelectorAll("tbody tr"))
        {
            var cells = row.Children.ToArray();
            if (cells.Length == 0 || row.ClassList.Contains("thead"))
            {
                continue;
            }

            var playerCell = cells[columnIndexes[PlayerColumn]];
            // Since the 2025-26 pages the body ends with a league-wide summary row.
            // Only that exact row is skipped; any other row without identity still fails.
            if (playerCell.TextContent.Trim() == LeagueAverageRow)
            {
                continue;
            }

            var playerId = playerCell.GetAttribute("data-append-csv")
                ?? ReadPlayerIdFromHref(playerCell);
            var playerName = playerCell.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(playerName))
            {
                throw new InvalidDataException(
                    $"Table '{tableId}' contains a player row without identity.");
            }

            var values = columnIndexes.ToDictionary(
                entry => entry.Key,
                entry => entry.Value < cells.Length
                    ? cells[entry.Value].TextContent.Trim()
                    : string.Empty,
                StringComparer.Ordinal);
            rows.Add(new ParsedRow(
                playerId,
                playerName,
                values,
                row.OuterHtml));
        }

        return SeasonTotalsOnly(rows, tableId);
    }

    /// <summary>
    /// A player traded mid-season has one row per team plus a combined row whose team
    /// reads "2TM", "3TM", …. The season line is the combined row; any other duplicate
    /// identity is still malformed.
    /// </summary>
    private static List<ParsedRow> SeasonTotalsOnly(List<ParsedRow> rows, string tableId) =>
        rows.GroupBy(row => row.ExternalPlayerId)
            .Select(group =>
            {
                if (group.Count() == 1)
                {
                    return group.Single();
                }

                var combined = group
                    .Where(row => CombinedTeams().IsMatch(row.Values.GetValueOrDefault(TeamColumn, string.Empty)))
                    .ToArray();
                return combined.Length == 1
                    ? combined[0]
                    : throw new InvalidDataException(
                        $"Table '{tableId}' repeats player '{group.Key}' without one combined-team row.");
            })
            .ToList();

    private static StatLine CreateStatLine(ParsedRow row) =>
        new(StatSourceColumnMap.SeasonTableCounting.ToDictionary(
            entry => entry.Value,
            entry => ParseDecimal(row, entry.Key)));

    private static void EnsureReboundIdentity(StatLine line, string playerId, decimal tolerance)
    {
        if (Math.Abs(line[StatKey.REB] - (line[StatKey.OREB] + line[StatKey.DREB])) > tolerance)
        {
            throw new InvalidDataException(
                $"Player '{playerId}' violates REB = OREB + DREB.");
        }
    }

    private static decimal ParseDecimal(ParsedRow row, string column)
    {
        var rawValue = row.Values[column];
        if (!decimal.TryParse(
                rawValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException(
                $"Column '{column}' for player '{row.ExternalPlayerId}' is not a decimal.");
        }

        return value;
    }

    private static int ParseInt32(ParsedRow row, string column)
    {
        var rawValue = row.Values[column];
        if (!int.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException(
                $"Column '{column}' for player '{row.ExternalPlayerId}' is not an integer.");
        }

        return value;
    }

    private static string? ReadPlayerIdFromHref(IElement playerCell)
    {
        var href = playerCell.QuerySelector("a")?.GetAttribute("href");
        return href is null ? null : Path.GetFileNameWithoutExtension(href);
    }

    private static IEnumerable<INode> Descendants(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed record ParsedRow(
        string ExternalPlayerId,
        string PlayerName,
        IReadOnlyDictionary<string, string> Values,
        string RawFragment);

    [GeneratedRegex("^[0-9]+TM$", RegexOptions.CultureInvariant)]
    private static partial Regex CombinedTeams();
}
