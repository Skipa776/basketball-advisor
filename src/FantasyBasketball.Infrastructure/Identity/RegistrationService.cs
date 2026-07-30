using System.Data;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Identity;

public sealed record RegistrationOutcome(
    FantasyUser? User,
    bool RegistrationClosed,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => User is not null;
}

public sealed class RegistrationService(
    FantasyDbContext database,
    UserManager<FantasyUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider)
{
    private const string OwnerRole = "Owner";

    public async Task<RegistrationOutcome> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var firstRegistration = !await database.Users.AnyAsync(cancellationToken);
        if (!firstRegistration && !options.Value.OpenRegistration)
        {
            return new RegistrationOutcome(null, true, []);
        }

        var user = new FantasyUser
        {
            Id = Guid.NewGuid(),
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
            IsInstanceOwner = firstRegistration,
            LockoutEnabled = true,
        };
        var createResult = await users.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return new RegistrationOutcome(
                null,
                false,
                createResult.Errors.Select(error => error.Description).ToArray());
        }

        if (firstRegistration)
        {
            await EnsureOwnerRoleAsync();
            var roleResult = await users.AddToRoleAsync(user, OwnerRole);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "The instance owner role could not be assigned.");
            }

            await ClaimUnownedRowsAsync(user.Id, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new RegistrationOutcome(user, false, []);
    }

    private async Task EnsureOwnerRoleAsync()
    {
        if (await roles.RoleExistsAsync(OwnerRole))
        {
            return;
        }

        var result = await roles.CreateAsync(new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = OwnerRole,
        });
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "The instance owner role could not be created.");
        }
    }

    private async Task ClaimUnownedRowsAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var sql = database.GetService<ISqlGenerationHelper>();
        var tables = database.Model.GetEntityTypes()
            .Where(entityType =>
                typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => new
            {
                Table = entityType.GetTableName()
                    ?? throw new InvalidOperationException(
                        $"Owned type '{entityType.ClrType.Name}' has no table."),
                Schema = entityType.GetSchema(),
            })
            .Distinct()
            .ToArray();
        foreach (var table in tables)
        {
            var identifier = sql.DelimitIdentifier(table.Table, table.Schema);
            await using var command = database.Database.GetDbConnection()
                .CreateCommand();
            command.Transaction = database.Database.CurrentTransaction
                ?.GetDbTransaction();
            command.CommandText =
                $"UPDATE {identifier} SET owner_id = @owner_id WHERE owner_id IS NULL";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "owner_id";
            parameter.Value = ownerId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
