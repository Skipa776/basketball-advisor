using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FantasyBasketball.Infrastructure.Persistence;

public sealed class FantasyDbContextFactory : IDesignTimeDbContextFactory<FantasyDbContext>
{
    public FantasyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FantasyDbContext>()
            .UseNpgsql("Host=localhost;Database=fantasy_design")
            .Options;
        return new FantasyDbContext(options);
    }
}
