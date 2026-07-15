using StackExchange.Redis;
namespace Nightfall.Bot;
public sealed class BotHeartbeatService(IConnectionMultiplexer redis) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) { await redis.GetDatabase().StringSetAsync("nightfall:bot-heartbeat", DateTime.UtcNow.ToString("O"), TimeSpan.FromMinutes(3)); await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
    }
}
