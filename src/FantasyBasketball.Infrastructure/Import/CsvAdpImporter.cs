using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Infrastructure.Import;

public sealed class CsvAdpImporter(
    string csv,
    TimeProvider timeProvider) : IAdpProvider
{
    private static readonly string[] RequiredHeader =
    [
        "external_id",
        "player_name",
        "adp",
        "standard_deviation",
    ];

    public string Name => DataSourceName.Csv;

    public DataSourceKind Kind => DataSourceKind.File;

    public Task<IReadOnlyList<AdpEntry>> GetAdpAsync(
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csv);
        cancellationToken.ThrowIfCancellationRequested();
        var lines = csv.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            throw new InvalidDataException("ADP CSV is empty.");
        }

        var headers = ParseRow(lines[0]);
        if (!headers.SequenceEqual(RequiredHeader, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"ADP CSV header must be '{string.Join(',', RequiredHeader)}'.");
        }

        IReadOnlyList<AdpEntry> entries = lines
            .Skip(1)
            .Select(ParseEntry)
            .ToArray();
        return Task.FromResult(entries);
    }

    private AdpEntry ParseEntry(string line)
    {
        var values = ParseRow(line);
        if (values.Count != RequiredHeader.Length)
        {
            throw new InvalidDataException(
                $"ADP CSV row must contain {RequiredHeader.Length} fields.");
        }

        return new AdpEntry(
            values[0],
            values[1],
            ParseDecimal(values[2], "adp"),
            string.IsNullOrWhiteSpace(values[3])
                ? null
                : ParseDecimal(values[3], "standard_deviation"),
            new DataProvenance(
                DataSourceName.Csv,
                values[0],
                timeProvider.GetUtcNow(),
                null,
                "csv-v1",
                DataSourceConfidence.CsvImport,
                Hash(line)));
    }

    private static IReadOnlyList<string> ParseRow(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(value.ToString().Trim());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("ADP CSV contains an unterminated quote.");
        }

        values.Add(value.ToString().Trim());
        return values;
    }

    private static decimal ParseDecimal(string raw, string column)
    {
        if (!decimal.TryParse(
                raw,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException(
                $"ADP CSV column '{column}' contains a non-decimal value.");
        }

        return value;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
