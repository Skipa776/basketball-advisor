using System.Text.Json;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Persistence;

public sealed partial class PersistenceTests
{
    [Fact]
    public async Task MV01_active_version_round_trips_every_field()
    {
        await using var database = new FantasyDbContext(options);
        var repository = new ModelVersionRepository(database);
        var version = ModelVersion("v1", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(version, TestContext.Current.CancellationToken);
        (await repository.GetActiveAsync("minutes-ridge", TestContext.Current.CancellationToken)).ShouldBeNull();

        await repository.ActivateAsync("minutes-ridge", "v1", TestContext.Current.CancellationToken);
        database.ChangeTracker.Clear();

        var active = await repository.GetActiveAsync("minutes-ridge", TestContext.Current.CancellationToken);
        active.ShouldNotBeNull();
        active.ModelName.ShouldBe("minutes-ridge");
        active.Version.ShouldBe("v1");
        active.FittedAt.ShouldBe(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        active.TrainSeasonEndYears.ShouldBe([2023, 2024, 2025]);
        // jsonb normalizes whitespace, so compare parsed documents rather than raw text.
        JsonSerializer.Serialize(JsonDocument.Parse(active.ParametersJson).RootElement)
            .ShouldBe(JsonSerializer.Serialize(JsonDocument.Parse(version.ParametersJson).RootElement));
        JsonSerializer.Serialize(JsonDocument.Parse(active.MetricsJson).RootElement)
            .ShouldBe(JsonSerializer.Serialize(JsonDocument.Parse(version.MetricsJson).RootElement));
        active.CardMarkdown.ShouldBe("# minutes-ridge v1");
    }

    [Fact]
    public async Task MV02_activating_a_second_version_deactivates_the_first()
    {
        await using var database = new FantasyDbContext(options);
        var repository = new ModelVersionRepository(database);
        await repository.AddAsync(ModelVersion("v1", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        await repository.AddAsync(ModelVersion("v2", new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        await repository.ActivateAsync("minutes-ridge", "v1", TestContext.Current.CancellationToken);
        await repository.ActivateAsync("minutes-ridge", "v2", TestContext.Current.CancellationToken);
        database.ChangeTracker.Clear();

        (await repository.GetActiveAsync("minutes-ridge", TestContext.Current.CancellationToken))!
            .Version.ShouldBe("v2");
        (await database.ModelVersions.CountAsync(
            row => row.IsActive, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task MV03_activating_an_unknown_version_throws_and_keeps_the_active_one()
    {
        await using var database = new FantasyDbContext(options);
        var repository = new ModelVersionRepository(database);
        await repository.AddAsync(ModelVersion("v1", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        await repository.ActivateAsync("minutes-ridge", "v1", TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => repository.ActivateAsync("minutes-ridge", "nope", TestContext.Current.CancellationToken));
        database.ChangeTracker.Clear();
        (await repository.GetActiveAsync("minutes-ridge", TestContext.Current.CancellationToken))!
            .Version.ShouldBe("v1");
    }

    [Fact]
    public async Task MV04_list_returns_newest_fitted_at_first()
    {
        await using var database = new FantasyDbContext(options);
        var repository = new ModelVersionRepository(database);
        await repository.AddAsync(ModelVersion("v1", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        await repository.AddAsync(ModelVersion("v3", new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        await repository.AddAsync(ModelVersion("v2", new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero)),
            TestContext.Current.CancellationToken);
        database.ChangeTracker.Clear();

        var listed = await repository.ListAsync("minutes-ridge", TestContext.Current.CancellationToken);
        listed.Select(version => version.Version).ShouldBe(["v3", "v2", "v1"]);
        (await repository.ListAsync("other-model", TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    private static ModelVersion ModelVersion(string version, DateTimeOffset fittedAt) => new(
        "minutes-ridge",
        version,
        fittedAt,
        [2023, 2024, 2025],
        "{\"coefficients\":[1.5,2.25]}",
        "{\"rmse\":4.1}",
        "# minutes-ridge v1");
}
