using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nightfall.Infrastructure.Agora;
using Nightfall.Infrastructure.History;
using Nightfall.Infrastructure.Sessions;
using StackExchange.Redis;

namespace Nightfall.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers Postgres (game history), Redis (live game sessions), and Agora token generation.
    /// Expects config keys: ConnectionStrings:Postgres, ConnectionStrings:Redis, Agora:AppId, Agora:AppCertificate.</summary>
    public static IServiceCollection AddNightfallInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Postgres");
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing required configuration: ConnectionStrings:Redis");

        services.AddDbContext<NightfallDbContext>(options => options.UseNpgsql(postgresConnectionString));
        services.AddScoped<IGameHistoryRepository, GameHistoryRepository>();

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<IKeyValueCache, RedisKeyValueCache>();
        services.AddSingleton<IGameSessionStore, GameSessionStore>();

        services.Configure<AgoraOptions>(configuration.GetSection(AgoraOptions.SectionName));
        services.AddSingleton<IAgoraTokenService, AgoraTokenService>();

        return services;
    }
}
