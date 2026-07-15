using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nightfall.Infrastructure.History;

/// <summary>Used only by `dotnet ef migrations add/database update` at design time — no live Postgres needed to build the model.</summary>
public sealed class NightfallDbContextFactory : IDesignTimeDbContextFactory<NightfallDbContext>
{
    public NightfallDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NightfallDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=nightfall;Username=nightfall;Password=nightfall");
        return new NightfallDbContext(optionsBuilder.Options);
    }
}
