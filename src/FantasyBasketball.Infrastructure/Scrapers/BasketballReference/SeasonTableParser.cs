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

public sealed class SeasonTableParser
{
    private const string PlayerColumn = "Player";
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
            EnsureReboundIdentity(perGameStats, perGameRow.ExternalPlayerId);
            EnsureReboundIdentity(totalStats, perGameRow.ExternalPlayerId);
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

        return rows;
    }

    private static StatLine CreateStatLine(ParsedRow row) =>
        new(StatSourceColumnMap.SeasonTableCounting.ToDictionary(
            entry => entry.Value,
            entry => ParseDecimal(row, entry.Key)));

    private static void EnsureReboundIdentity(StatLine line, string playerId)
    {
        if (line[StatKey.REB] != line[StatKey.OREB] + line[StatKey.DREB])
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
}
