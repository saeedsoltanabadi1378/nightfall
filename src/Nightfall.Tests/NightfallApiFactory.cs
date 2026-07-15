using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nightfall.Infrastructure.History;
using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Tests;

/// <summary>
/// Boots the real Nightfall.Api pipeline (real routing, real JWT auth, real GameService/Domain
/// logic) but swaps Postgres for EF Core's InMemory provider and Redis for an in-memory fake —
/// neither is available in this sandbox (see ebinaa-npgsql-ormlite-sandbox memory re: no local
/// Postgres, and no local Redis either). InMemory rather than Sqlite here specifically: EF Core
/// throws ("only a single database provider can be registered") if both Npgsql and Sqlite —
/// both "relational" providers — end up registered in the same container, which happens once the
/// app's own Program.cs has already added Npgsql and this factory tries to layer Sqlite on top.
/// InMemory uses a separate non-relational code path and doesn't conflict. Schema/mapping
/// behavior against a real relational provider is separately covered by
/// GameHistoryRepositoryTests, which builds its own Sqlite DbContext from scratch.
/// </summary>
public sealed class NightfallApiFactory : WebApplicationFactory<Program>
{
    public const string TestBotToken = "123456789:AAFakeTestTokenForGoldenFixtureOnly1234";
    public const string TestJwtSigningKey = "test-signing-key-at-least-32-characters-long-for-hs256!!";

    private readonly string _databaseName = $"nightfall-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestJwtSigningKey,
                ["Jwt:Issuer"] = "nightfall",
                ["Jwt:Audience"] = "nightfall-clients",
                ["Telegram:BotToken"] = TestBotToken,
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["ConnectionStrings:Redis"] = "localhost:0,abortConnect=false",
                ["Agora:AppId"] = "0123456789abcdef0123456789abcdef",
                ["Agora:AppCertificate"] = "fedcba9876543210fedcba9876543210"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Removing just DbContextOptions<NightfallDbContext> isn't enough: EF Core detects
            // "multiple providers registered" by scanning the *whole* container for provider
            // marker services (e.g. Npgsql's), not just the options for this one DbContext type.
            // Strip every EF Core / Npgsql-provider-specific descriptor Program.cs's own
            // AddNightfallInfrastructure added, then re-add fresh with the InMemory provider.
            var efProviderDescriptors = services
                .Where(d =>
                    (d.ServiceType.Assembly.GetName().Name ?? "").Contains("EntityFrameworkCore") ||
                    (d.ServiceType.Assembly.GetName().Name ?? "").Contains("Npgsql") ||
                    (d.ImplementationType?.Assembly.GetName().Name ?? "").Contains("EntityFrameworkCore") ||
                    (d.ImplementationType?.Assembly.GetName().Name ?? "").Contains("Npgsql"))
                .ToList();
            foreach (var descriptor in efProviderDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<NightfallDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IGameSessionStore>();
            services.RemoveAll<IKeyValueCache>();
            services.AddSingleton<IKeyValueCache, InMemoryKeyValueCache>();
            services.AddSingleton<IGameSessionStore, GameSessionStore>();
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NightfallDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
