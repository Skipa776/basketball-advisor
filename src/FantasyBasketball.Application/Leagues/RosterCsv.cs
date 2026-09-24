namespace FantasyBasketball.Application.Leagues;

public sealed record RosterCsvRow(int Line, string Team, string Player, bool Mine);

/// <summary>
/// The guaranteed roster import rung: <c>Team,Player[,Mine]</c> with a header row.
/// Quoted fields (as platform exports write them) are supported; anything malformed
/// fails with its line number rather than being guessed at.
/// </summary>
public static class RosterCsv
{
    public const int MaximumLength = 64 * 1024;
    public const string Template = "Team,Player,Mine\nMy Team,Nikola Jokic,yes\nMy Team,Stephen Curry,yes\nRival Team,Luka Doncic,\n";

    public static IReadOnlyList<RosterCsvRow> Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv) || csv.Length > MaximumLength)
        {
            throw new FormatException($"Paste a roster CSV of at most {MaximumLength / 1024} KB.");
        }

        var lines = csv.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var header = Fields(lines[0], 1).Select(field => field.ToLowerInvariant()).ToArray();
        var team = Array.IndexOf(header, "team");
        var player = Array.IndexOf(header, "player");
        var mine = Array.IndexOf(header, "mine");
        if (team < 0 || player < 0)
        {
            throw new FormatException("The first line must be a header with Team and Player columns.");
        }

        var rows = new List<RosterCsvRow>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var fields = Fields(lines[index], index + 1);
            string At(int column) => column >= 0 && column < fields.Count ? fields[column] : string.Empty;
            if (At(team).Length == 0 || At(player).Length == 0)
            {
                throw new FormatException($"Line {index + 1} needs both a team and a player.");
            }

            rows.Add(new RosterCsvRow(index + 1, At(team), At(player),
                At(mine).ToLowerInvariant() is "yes" or "y" or "true" or "1" or "x"));
        }

        return rows.Count == 0 ? throw new FormatException("The CSV has no roster rows.") : rows;
    }

    private static List<string> Fields(string line, int lineNumber)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quoted && character == '"' && index + 1 < line.Length && line[index + 1] == '"')
            {
                current.Append('"');
                index++;
            }
            else if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        if (quoted)
        {
            throw new FormatException($"Line {lineNumber} has an unclosed quote.");
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }
}
