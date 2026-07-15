using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nightfall.Infrastructure.History;

namespace Nightfall.Infrastructure.Admin;

public sealed record BotSettingsSnapshot(int Version, int MinPlayers, int MaxPlayers, bool MaintenanceMode, string MaintenanceMessage, IReadOnlySet<string> EnabledCommands, bool SoloTestEnabled, string MiniAppBaseUrl, string HelpMessage, string WelcomeMessage, DateTime UpdatedAt);
public interface IBotSettingsService { Task<BotSettingsSnapshot> GetAsync(CancellationToken ct = default); }

public sealed class BotSettingsService : IBotSettingsService
{
    private readonly IServiceScopeFactory _scopes;
    private BotSettingsSnapshot? _cached;
    private DateTime _expires;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public BotSettingsService(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<BotSettingsSnapshot> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null && _expires > DateTime.UtcNow) return _cached;
        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null && _expires > DateTime.UtcNow) return _cached;
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NightfallDbContext>();
            var row = await db.BotSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
            if (row is null) { row = new(); db.BotSettings.Add(row); await db.SaveChangesAsync(ct); }
            _cached = ToSnapshot(row); _expires = DateTime.UtcNow.AddSeconds(3); return _cached;
        }
        finally { _gate.Release(); }
    }

    public static BotSettingsSnapshot ToSnapshot(BotSettingsRecord row) => new(row.Version, row.MinPlayers, row.MaxPlayers, row.MaintenanceMode, row.MaintenanceMessage, JsonSerializer.Deserialize<HashSet<string>>(row.EnabledCommandsJson) ?? [], row.SoloTestEnabled, row.MiniAppBaseUrl, row.HelpMessage, row.WelcomeMessage, row.UpdatedAt);
}
