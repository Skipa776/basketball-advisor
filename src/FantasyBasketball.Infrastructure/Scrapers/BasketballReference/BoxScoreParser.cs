using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Infrastructure.Scrapers.BasketballReference;

public sealed record ParsedBoxScorePlayer(
    string ExternalPlayerId, string PlayerName, string TeamCode, bool DidPlay, StatLine? Statistics);

public sealed record ParsedBoxScore(
    string ExternalGameId, DateOnly PlayedOn, string RawRecordHash,
    ReadOnlyCollection<ParsedBoxScorePlayer> Players);

/// <summary>Parses supplied HTML only. Does not fetch or authorize any URL.</summary>
public sealed partial class BoxScoreParser
{
    public static string ParserVersion { get; } = $"{DataSourceName.BasketballReference}-v1";
    private static readonly HashSet<string> NonAppearanceReasons = new(StringComparer.Ordinal)
    {
        "Did Not Play", "Did Not Dress", "Inactive", "Not With Team", "Player Suspended",
    };

    public ParsedBoxScore Parse(string html, string expectedExternalGameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedExternalGameId);
        if (!GameIdPattern().IsMatch(expectedExternalGameId)
            || !DateOnly.TryParseExact(expectedExternalGameId.AsSpan(0, 8), "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var playedOn))
        {
            throw new ArgumentException("Expected a dated provider box-score game ID.", nameof(expectedExternalGameId));
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html))).ToLowerInvariant();
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var canonicalLinks = document.QuerySelectorAll("link[rel=canonical]");
        if (canonicalLinks.Length != 1
            || !Uri.TryCreate(BbrefUrlBuilder.BaseUri, canonicalLinks[0].GetAttribute("href"), out var canonical)
            || canonical.Scheme != Uri.UriSchemeHttps || canonical.Host != BbrefUrlBuilder.BaseUri.Host
            || !canonical.IsDefaultPort || canonical.UserInfo.Length != 0
            || canonical.Query.Length != 0 || canonical.Fragment.Length != 0
            || canonical.AbsolutePath != $"/boxscores/{expectedExternalGameId}.html")
        {
            throw new InvalidDataException("The canonical page does not match the expected game.");
        }

        var tables = FindTables(document).ToList();
        foreach (var comment in Descendants(document).OfType<IComment>())
        {
            tables.AddRange(FindTables(parser.ParseDocument(comment.Data)));
        }

        if (tables.Count != 2 || tables.Select(table => table.Id).Distinct().Count() != 2)
        {
            throw new InvalidDataException("Expected exactly two distinct full-game basic box-score tables.");
        }

        var players = new List<ParsedBoxScorePlayer>();
        foreach (var table in tables)
        {
            var headers = table.QuerySelectorAll("thead [data-stat]")
                .Select(cell => cell.GetAttribute("data-stat")!).ToHashSet(StringComparer.Ordinal);
            foreach (var column in StatSourceColumnMap.BoxScoreCounting.Keys.Prepend("player"))
            {
                if (!headers.Contains(column)) throw new InvalidDataException($"Missing box-score column '{column}'.");
            }

            var team = TablePattern().Match(table.Id!).Groups[1].Value;
            var before = players.Count;
            foreach (var row in table.QuerySelectorAll("tbody tr"))
            {
                if (row.ClassList.Contains("thead") || row.Children.Length == 0) continue;
                var cells = row.Children.Where(cell => cell.HasAttribute("data-stat")).ToArray();
                if (cells.GroupBy(cell => cell.GetAttribute("data-stat")).Any(group => group.Count() != 1))
                    throw new InvalidDataException("Duplicate box-score row column.");
                var columns = cells.ToDictionary(cell => cell.GetAttribute("data-stat")!, StringComparer.Ordinal);
                if (!columns.TryGetValue("player", out var identity)) throw new InvalidDataException("Missing player identity.");
                var slug = identity.GetAttribute("data-append-csv");
                var href = identity.QuerySelector("a")?.GetAttribute("href");
                var linkSlug = href is null ? null : Path.GetFileNameWithoutExtension(href);
                if (slug is not null && linkSlug is not null && slug != linkSlug)
                    throw new InvalidDataException("Conflicting player identity in box-score row.");
                slug ??= linkSlug;
                var name = identity.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(slug) || !PlayerIdPattern().IsMatch(slug) || name.Length == 0)
                    throw new InvalidDataException("Missing or malformed player identity.");

                StatLine? statistics = null;
                if (columns.TryGetValue("reason", out var reason))
                {
                    if (!NonAppearanceReasons.Contains(reason.TextContent.Trim())
                        || StatSourceColumnMap.BoxScoreCounting.Keys.Any(columns.ContainsKey))
                        throw new InvalidDataException($"Unrecognized or contradictory non-appearance row for '{slug}'.");
                }
                else
                {
                    var values = new Dictionary<StatKey, decimal>();
                    foreach (var (column, stat) in StatSourceColumnMap.BoxScoreCounting)
                    {
                        if (!columns.TryGetValue(column, out var cell))
                            throw new InvalidDataException($"Missing box-score column '{column}' for '{slug}'.");
                        values[stat] = ParseNumber(cell.TextContent.Trim(), column, stat == StatKey.MIN);
                    }
                    statistics = new StatLine(values);
                    ValidateStatistics(statistics);
                }
                players.Add(new ParsedBoxScorePlayer(slug, name, team, statistics is not null, statistics));
            }
            if (!players.Skip(before).Any(player => player.DidPlay))
                throw new InvalidDataException($"No completed appearances in team table '{team}'.");
        }

        if (players.Select(player => player.ExternalPlayerId).Distinct().Count() != players.Count)
            throw new InvalidDataException("Duplicate player identity in the game.");
        return new ParsedBoxScore(expectedExternalGameId, playedOn, hash, players.AsReadOnly());
    }

    private static decimal ParseNumber(string text, string column, bool minutes)
    {
        if (minutes)
        {
            var parts = text.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var whole)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                && whole >= 0 && seconds is >= 0 and < 60)
                return whole + seconds / 60m;
        }
        else if (decimal.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 0m)
        {
            return count;
        }
        throw new InvalidDataException($"Invalid box-score number in '{column}': '{text}'.");
    }

    private static void ValidateStatistics(StatLine line)
    {
        if (line[StatKey.REB] != line[StatKey.OREB] + line[StatKey.DREB]
            || line[StatKey.FGM] > line[StatKey.FGA] || line[StatKey.FTM] > line[StatKey.FTA]
            || line[StatKey.FG3M] > line[StatKey.FG3A] || line[StatKey.FG3M] > line[StatKey.FGM]
            || line[StatKey.FG3A] > line[StatKey.FGA])
            throw new InvalidDataException("Box-score rebound or shooting identity is invalid.");
    }

    private static IEnumerable<IElement> FindTables(IDocument document) =>
        document.QuerySelectorAll("table[id]").Where(table => TablePattern().IsMatch(table.Id!));

    private static IEnumerable<INode> Descendants(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [GeneratedRegex("^[0-9]{8}0[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex GameIdPattern();
    [GeneratedRegex("^box-([A-Z0-9]{3})-game-basic$", RegexOptions.CultureInvariant)]
    private static partial Regex TablePattern();
    [GeneratedRegex("^[a-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PlayerIdPattern();
}
