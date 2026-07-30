using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace FantasyBasketball.Infrastructure.Identity;

public sealed record OwnedTableArchive(string Table, JsonElement Rows);

public sealed record OwnedDataArchive(
    int Version,
    IReadOnlyList<OwnedTableArchive> Tables);

public sealed class InvalidAccountArchiveException(string message)
    : Exception(message);

public sealed class AccountDataService(
    FantasyDbContext database,
    IUserContext userContext)
{
    private const int ArchiveVersion = 1;

    public async Task<OwnedDataArchive> ExportAsync(
        CancellationToken cancellationToken)
    {
        var userId = userContext.CurrentUserId;
        var tables = OwnedTablesInDependencyOrder();
        var archives = new List<OwnedTableArchive>(tables.Count);
        await database.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var table in tables)
            {
                var rows = await ReadOwnedRowsAsync(
                    table,
                    userId,
                    cancellationToken);
                archives.Add(new OwnedTableArchive(
                    table,
                    TransformOwner(rows, null)));
            }
        }
        finally
        {
            await database.Database.CloseConnectionAsync();
        }

        return new OwnedDataArchive(ArchiveVersion, archives);
    }

    public async Task ImportAsync(
        OwnedDataArchive archive,
        CancellationToken cancellationToken)
    {
        var userId = userContext.CurrentUserId;
        var tables = OwnedTablesInDependencyOrder();
        ValidateArchiveShape(archive, tables);
        await using var transaction = await database.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        foreach (var table in tables)
        {
            if (await CountOwnedRowsAsync(table, userId, cancellationToken) != 0)
            {
                throw new InvalidAccountArchiveException(
                    "Account data can only be imported into an empty account.");
            }
        }

        var importedRows = await PrepareImportedRowsAsync(
            archive,
            userId,
            cancellationToken);
        foreach (var table in tables)
        {
            var ownedRows = importedRows[table];
            if (ownedRows.GetArrayLength() == 0)
            {
                continue;
            }

            await using var command = CreateCommand(
                $"""
                INSERT INTO {QuoteIdentifier(table)}
                SELECT *
                FROM jsonb_populate_recordset(
                    NULL::{QuoteIdentifier(table)},
                    @rows::jsonb);
                """);
            AddParameter(command, "rows", ownedRows.GetRawText());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var userId = userContext.CurrentUserId;
        var user = await database.Users.SingleOrDefaultAsync(
            value => value.Id == userId,
            cancellationToken);
        if (user is null)
        {
            throw new ResourceNotFoundException(
                "The requested account was not found.");
        }

        database.Users.Remove(user);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<JsonElement> ReadOwnedRowsAsync(
        string table,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            $"""
            SELECT COALESCE(
                jsonb_agg(to_jsonb(owned_row) ORDER BY owned_row.id),
                '[]'::jsonb)::text
            FROM {QuoteIdentifier(table)} AS owned_row
            WHERE owned_row.owner_id = @owner_id;
            """);
        AddParameter(command, "owner_id", userId);
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? "[]";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private async Task<long> CountOwnedRowsAsync(
        string table,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            $"""
            SELECT COUNT(*)
            FROM {QuoteIdentifier(table)}
            WHERE owner_id = @owner_id;
            """);
        AddParameter(command, "owner_id", userId);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private DbCommand CreateCommand(string commandText)
    {
        var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        if (database.Database.CurrentTransaction is { } transaction)
        {
            command.Transaction = transaction.GetDbTransaction();
        }

        return command;
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private IReadOnlyList<string> OwnedTablesInDependencyOrder()
    {
        var pending = database.Model.GetEntityTypes()
            .Where(entityType =>
                typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .ToHashSet();
        var ordered = new List<IEntityType>(pending.Count);
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(entityType => entityType.GetForeignKeys().All(
                    foreignKey => !pending.Contains(
                        foreignKey.PrincipalEntityType)))
                .OrderBy(entityType => entityType.GetTableName())
                .ToArray();
            if (ready.Length == 0)
            {
                throw new InvalidOperationException(
                    "Owned account data contains a circular dependency.");
            }

            ordered.AddRange(ready);
            pending.ExceptWith(ready);
        }

        return ordered
            .Select(entityType => entityType.GetTableName()
                ?? throw new InvalidOperationException(
                    $"Owned entity {entityType.Name} is not mapped to a table."))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonElement TransformOwner(
        JsonElement rows,
        string? ownerId)
    {
        var array = JsonNode.Parse(rows.GetRawText())?.AsArray()
            ?? throw new InvalidOperationException(
                "Owned table archive rows must be a JSON array.");
        foreach (var node in array)
        {
            var row = node?.AsObject()
                ?? throw new InvalidOperationException(
                    "Owned table archive rows must be JSON objects.");
            row["owner_id"] = ownerId;
        }

        return JsonDocument.Parse(array.ToJsonString()).RootElement.Clone();
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>>
        PrepareImportedRowsAsync(
        OwnedDataArchive archive,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tableRows = archive.Tables.ToDictionary(
            table => table.Table,
            table => JsonNode.Parse(table.Rows.GetRawText())?.AsArray()
                ?? throw new InvalidAccountArchiveException(
                    "Owned table archive rows must be JSON arrays."),
            StringComparer.Ordinal);
        var sourceIds = tableRows.Values
            .SelectMany(rows => rows)
            .Select(row => row?.AsObject()["id"]?.GetValue<string>()
                ?? throw new InvalidAccountArchiveException(
                    "Every owned archive row must have an id."))
            .ToArray();
        if (sourceIds.Any(id => !Guid.TryParse(id, out _))
            || sourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != sourceIds.Length)
        {
            throw new InvalidAccountArchiveException(
                "Owned archive row ids must be unique GUIDs.");
        }

        var idMap = sourceIds
            .ToDictionary(
                id => id,
                _ => Guid.NewGuid().ToString(),
                StringComparer.OrdinalIgnoreCase);
        await ValidateOwnedReferencesAsync(
            tableRows,
            idMap,
            cancellationToken);

        foreach (var row in tableRows.Values.SelectMany(rows => rows))
        {
            var fields = row?.AsObject()
                ?? throw new InvalidAccountArchiveException(
                    "Owned table archive rows must be JSON objects.");
            foreach (var property in fields.ToArray())
            {
                if (property.Key == "owner_id")
                {
                    fields[property.Key] = userId.ToString();
                    continue;
                }

                if (property.Key is "reviewed_by_user_id"
                    or "verified_by_user_id"
                    or "override_user_id")
                {
                    if (property.Value is not null)
                    {
                        fields[property.Key] = userId.ToString();
                    }

                    continue;
                }

                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && text is not null
                    && idMap.TryGetValue(text, out var remapped))
                {
                    fields[property.Key] = remapped;
                    continue;
                }

                if (property.Value is JsonArray array)
                {
                    for (var index = 0; index < array.Count; index++)
                    {
                        if (array[index] is JsonValue item
                            && item.TryGetValue<string>(out var itemText)
                            && itemText is not null
                            && idMap.TryGetValue(itemText, out var remappedItem))
                        {
                            array[index] = remappedItem;
                        }
                    }
                }
            }
        }

        return tableRows.ToDictionary(
            pair => pair.Key,
            pair => JsonDocument.Parse(pair.Value.ToJsonString())
                .RootElement
                .Clone(),
            StringComparer.Ordinal);
    }

    private async Task ValidateOwnedReferencesAsync(
        IReadOnlyDictionary<string, JsonArray> tableRows,
        IReadOnlyDictionary<string, string> idMap,
        CancellationToken cancellationToken)
    {
        foreach (var rule in OwnedForeignKeyRules())
        {
            foreach (var node in tableRows[rule.Table])
            {
                var value = node?.AsObject()[rule.Column];
                if (value is null)
                {
                    continue;
                }

                var id = value.GetValue<string>();
                if (idMap.ContainsKey(id))
                {
                    continue;
                }

                if (!rule.AllowsGlobal
                    || !Guid.TryParse(id, out var globalId)
                    || !await GlobalRowExistsAsync(
                        rule.PrincipalTable,
                        globalId,
                        cancellationToken))
                {
                    throw new InvalidAccountArchiveException(
                        $"Owned reference '{rule.Table}.{rule.Column}' "
                        + "does not belong to the archive.");
                }
            }
        }

        foreach (var node in tableRows["adjusted_projection"])
        {
            var applied = node?.AsObject()["applied_context_event_ids"]
                ?.AsArray() ?? [];
            if (applied.Any(item =>
                item is null
                || !idMap.ContainsKey(item.GetValue<string>())))
            {
                throw new InvalidAccountArchiveException(
                    "An adjusted projection references context outside the archive.");
            }
        }
    }

    private async Task<bool> GlobalRowExistsAsync(
        string table,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            $"""
            SELECT EXISTS (
                SELECT 1
                FROM {QuoteIdentifier(table)}
                WHERE id = @id AND owner_id IS NULL);
            """);
        AddParameter(command, "id", id);
        return Convert.ToBoolean(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private IReadOnlyList<OwnedForeignKeyRule> OwnedForeignKeyRules()
    {
        var rules = new List<OwnedForeignKeyRule>();
        foreach (var entityType in database.Model.GetEntityTypes().Where(
            entityType =>
                typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType)))
        {
            var table = entityType.GetTableName()
                ?? throw new InvalidOperationException(
                    $"Owned entity {entityType.Name} has no table.");
            var store = StoreObjectIdentifier.Table(
                table,
                entityType.GetSchema());
            foreach (var foreignKey in entityType.GetForeignKeys().Where(
                foreignKey => typeof(IOwnedResource).IsAssignableFrom(
                    foreignKey.PrincipalEntityType.ClrType)))
            {
                if (foreignKey.Properties.Count != 1)
                {
                    throw new InvalidOperationException(
                        "Composite owned foreign keys are unsupported.");
                }

                var principalTable = foreignKey.PrincipalEntityType.GetTableName()
                    ?? throw new InvalidOperationException(
                        "Owned principal is not mapped to a table.");
                rules.Add(new OwnedForeignKeyRule(
                    table,
                    foreignKey.Properties[0].GetColumnName(store)
                        ?? throw new InvalidOperationException(
                            "Owned foreign key has no column."),
                    principalTable,
                    typeof(IGlobalOrOwnedResource).IsAssignableFrom(
                        foreignKey.PrincipalEntityType.ClrType)));
            }
        }

        return rules;
    }

    private static void ValidateArchiveShape(
        OwnedDataArchive archive,
        IReadOnlyList<string> expectedTables)
    {
        if (archive.Version != ArchiveVersion
            || archive.Tables.Count != expectedTables.Count
            || archive.Tables.Select(table => table.Table)
                .Distinct(StringComparer.Ordinal)
                .Count() != expectedTables.Count
            || expectedTables.Any(expected =>
                archive.Tables.All(table => table.Table != expected))
            || archive.Tables.Any(table =>
                table.Rows.ValueKind != JsonValueKind.Array))
        {
            throw new InvalidAccountArchiveException(
                "The account archive is invalid or unsupported.");
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (identifier.Length == 0
            || identifier.Any(character =>
                character is not (>= 'a' and <= 'z') and not '_'))
        {
            throw new InvalidOperationException(
                "Owned table metadata contains an unsafe identifier.");
        }

        return $"\"{identifier}\"";
    }

    private sealed record OwnedForeignKeyRule(
        string Table,
        string Column,
        string PrincipalTable,
        bool AllowsGlobal);
}
