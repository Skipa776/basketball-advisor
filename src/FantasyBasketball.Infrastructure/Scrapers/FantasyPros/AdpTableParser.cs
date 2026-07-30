using System.Collections.ObjectModel;
using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace FantasyBasketball.Infrastructure.Scrapers.FantasyPros;

public sealed record ParsedAdpEntry(
    string ExternalPlayerId,
    string PlayerName,
    decimal AverageDraftPosition,
    decimal? StandardDeviation,
    string RawFragment);

public sealed class AdpTableParser
{
    private const string PlayerColumn = "Player";
    private const string AverageColumn = "AVG";
    private const string StandardDeviationColumn = "STD DEV";

    public IReadOnlyList<ParsedAdpEntry> Parse(string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        var document = new HtmlParser().ParseDocument(html);
        var table = document.QuerySelector("table#data")
            ?? throw new InvalidDataException("Missing expected ADP table 'data'.");
        var headers = table.QuerySelectorAll("thead tr").LastOrDefault()?
            .Children
            .Select(cell => cell.TextContent.Trim())
            .ToArray()
            ?? [];
        var columnIndexes = headers
            .Select((header, index) => (header, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.header))
            .ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);

        foreach (var required in new[]
                 {
                     PlayerColumn,
                     AverageColumn,
                     StandardDeviationColumn,
                 })
        {
            if (!columnIndexes.ContainsKey(required))
            {
                throw new InvalidDataException(
                    $"Missing expected column '{required}' in ADP table.");
            }
        }

        var entries = new List<ParsedAdpEntry>();
        foreach (var row in table.QuerySelectorAll("tbody tr"))
        {
            var cells = row.Children.ToArray();
            if (cells.Length == 0)
            {
                continue;
            }

            var playerCell = ReadCell(cells, columnIndexes[PlayerColumn], PlayerColumn);
            var playerName = playerCell.TextContent.Trim();
            var href = playerCell.QuerySelector("a")?.GetAttribute("href");
            var externalId = href is null
                ? null
                : Path.GetFileNameWithoutExtension(href);
            if (string.IsNullOrWhiteSpace(externalId)
                || string.IsNullOrWhiteSpace(playerName))
            {
                throw new InvalidDataException(
                    "ADP table contains a player row without identity.");
            }

            entries.Add(new ParsedAdpEntry(
                externalId,
                playerName,
                ParseDecimal(cells, columnIndexes[AverageColumn], AverageColumn),
                ParseNullableDecimal(
                    cells,
                    columnIndexes[StandardDeviationColumn],
                    StandardDeviationColumn),
                row.OuterHtml));
        }

        return new ReadOnlyCollection<ParsedAdpEntry>(entries);
    }

    private static IElement ReadCell(
        IReadOnlyList<IElement> cells,
        int index,
        string column) =>
        index < cells.Count
            ? cells[index]
            : throw new InvalidDataException(
                $"ADP row is missing expected column '{column}'.");

    private static decimal ParseDecimal(
        IReadOnlyList<IElement> cells,
        int index,
        string column)
    {
        var raw = ReadCell(cells, index, column).TextContent.Trim();
        if (!decimal.TryParse(
                raw,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException(
                $"ADP column '{column}' contains a non-decimal value.");
        }

        return value;
    }

    private static decimal? ParseNullableDecimal(
        IReadOnlyList<IElement> cells,
        int index,
        string column)
    {
        var raw = ReadCell(cells, index, column).TextContent.Trim();
        return string.IsNullOrEmpty(raw)
            ? null
            : ParseDecimal(cells, index, column);
    }
}
