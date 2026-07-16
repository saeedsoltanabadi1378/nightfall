using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nightfall.Api.Games;
using Nightfall.Infrastructure.Admin;
using Nightfall.Infrastructure.History;
using Nightfall.Infrastructure.Sessions;
using StackExchange.Redis;

namespace Nightfall.Api.Admin;

public static class AdminEndpoints
{
    private const string CsrfCookie = "nightfall-admin-csrf";
    private static bool ValidCsrf(HttpContext c) => c.Request.Cookies.TryGetValue(CsrfCookie, out var cookie) && c.Request.Headers.TryGetValue("X-CSRF-Token", out var header) && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(cookie), System.Text.Encoding.UTF8.GetBytes(header.ToString()));

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/admin/auth");
        auth.MapPost("/login", async (LoginRequest request, HttpContext http, IOptions<AdminOptions> options) =>
        {
            var configured = options.Value;
            if (string.IsNullOrWhiteSpace(configured.Username) || string.IsNullOrEmpty(configured.Password) || !string.Equals(request.Username, configured.Username, StringComparison.Ordinal) || !string.Equals(request.Password, configured.Password, StringComparison.Ordinal)) return Results.Unauthorized();
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, configured.Username), new Claim(ClaimTypes.Role, "Admin")], CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
            return Results.Ok(new { username = configured.Username });
        }).RequireRateLimiting("admin-login").AllowAnonymous();
        auth.MapGet("/session", (HttpContext http) => Results.Ok(new { authenticated = http.User.Identity?.IsAuthenticated == true, username = http.User.Identity?.Name })).RequireAuthorization("AdminOnly");
        auth.MapGet("/csrf", (HttpContext http) => { var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); http.Response.Cookies.Append(CsrfCookie, token, new CookieOptions { HttpOnly = false, Secure = !http.Request.Host.Host.StartsWith("localhost"), SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromHours(8) }); return Results.Ok(new { token }); }).RequireAuthorization("AdminOnly");
        auth.MapPost("/logout", async (HttpContext http) => { if (!ValidCsrf(http)) return Results.BadRequest(new { detail = "Invalid CSRF token." }); await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); http.Response.Cookies.Delete(CsrfCookie); return Results.Ok(); }).RequireAuthorization("AdminOnly");

        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminOnly");
        group.MapGet("/overview", async (NightfallDbContext db, IConnectionMultiplexer redis) => Results.Ok(new {
            activeGames = await CountActiveAsync(redis), completedGames = await db.Games.CountAsync(x => x.Status == "Completed"), cancelledGames = await db.Games.CountAsync(x => x.Status == "Cancelled"), users = await db.UserProfiles.CountAsync(), chats = await db.ChatProfiles.CountAsync(), recentEvents = await db.OperationalEvents.OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
        }));
        group.MapGet("/games", async (string? status, int? page, int? pageSize, NightfallDbContext db, IConnectionMultiplexer redis, IGameSessionStore sessions) =>
        {
            int p = Math.Max(1, page ?? 1), size = Math.Clamp(pageSize ?? 25, 1, 100);
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) { var games = await LoadActiveAsync(redis, sessions); return Results.Ok(new { items = games.Select(g => g.ToSnapshot()), total = games.Count, page = p, pageSize = size }); }
            var query = db.Games.AsNoTracking().OrderByDescending(x => x.EndedAt);
            var total = await query.CountAsync();
            var items = await query.Skip((p - 1) * size).Take(size).Select(game => new AdminGameView(
                game.Id, game.TelegramChatId, game.CreatedAt, game.EndedAt, game.Result, game.Status,
                game.CancellationReason,
                game.Players.Select(player => new AdminGamePlayerView(
                    player.PlayerId, player.TelegramUserId, player.TelegramUsername, player.Role,
                    player.SurvivedToEnd, player.GodfatherRank)).ToList())).ToListAsync();
            return Results.Ok(new { items, total, page = p, pageSize = size });
        });
        group.MapGet("/games/{id:guid}", async (Guid id, IGameSessionStore sessions, NightfallDbContext db) =>
        {
            var live = await sessions.GetAsync(id);
            if (live is not null) return Results.Ok(live.ToSnapshot());
            var past = await db.Games.AsNoTracking().Where(game => game.Id == id).Select(game => new AdminGameView(
                game.Id, game.TelegramChatId, game.CreatedAt, game.EndedAt, game.Result, game.Status,
                game.CancellationReason,
                game.Players.Select(player => new AdminGamePlayerView(
                    player.PlayerId, player.TelegramUserId, player.TelegramUsername, player.Role,
                    player.SurvivedToEnd, player.GodfatherRank)).ToList())).SingleOrDefaultAsync();
            return past is not null ? Results.Ok(past) : Results.NotFound();
        });
        group.MapPost("/games/{id:guid}/cancel", async (Guid id, CancelRequest request, HttpContext http, GameService games) => { if (!ValidCsrf(http)) return Results.BadRequest(new { detail = "Invalid CSRF token." }); if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { detail = "A cancellation reason is required." }); return await games.CancelByAdminAsync(id, request.Reason, http.User.Identity?.Name ?? "admin") ? Results.Ok() : Results.NotFound(); });
        group.MapGet("/players", async (int? page, int? pageSize, string? search, NightfallDbContext db) => { int p=Math.Max(1,page??1), size=Math.Clamp(pageSize??25,1,100); var q=db.UserProfiles.AsNoTracking().Where(x => search == null || x.Username.ToLower().Contains(search.ToLower())); return Results.Ok(new { items=await q.OrderByDescending(x=>x.LastSeenAt).Skip((p-1)*size).Take(size).ToListAsync(), total=await q.CountAsync(), page=p,pageSize=size }); });
        group.MapGet("/chats", async (int? page, int? pageSize, NightfallDbContext db) => { int p=Math.Max(1,page??1), size=Math.Clamp(pageSize??25,1,100); var q=db.ChatProfiles.AsNoTracking(); return Results.Ok(new { items=await q.OrderByDescending(x=>x.LastSeenAt).Skip((p-1)*size).Take(size).ToListAsync(), total=await q.CountAsync(),page=p,pageSize=size }); });
        group.MapGet("/events", async (int? page, int? pageSize, string? category, NightfallDbContext db) => { int p=Math.Max(1,page??1),size=Math.Clamp(pageSize??25,1,100); var q=db.OperationalEvents.AsNoTracking().Where(x=>category==null||x.Category==category); return Results.Ok(new { items=await q.OrderByDescending(x=>x.CreatedAt).Skip((p-1)*size).Take(size).ToListAsync(),total=await q.CountAsync(),page=p,pageSize=size }); });
        group.MapGet("/health", async (NightfallDbContext db, IConnectionMultiplexer redis) => { bool pg, rd; try { pg=await db.Database.CanConnectAsync(); } catch { pg=false; } try { rd=await redis.GetDatabase().PingAsync()<TimeSpan.FromSeconds(2); } catch { rd=false; } var heartbeat=await redis.GetDatabase().StringGetAsync("nightfall:bot-heartbeat"); var fresh=DateTime.TryParse(heartbeat,out var h)&&h>DateTime.UtcNow.AddMinutes(-2); return Results.Ok(new { api="healthy", postgres=pg?"healthy":"unhealthy", redis=rd?"healthy":"unhealthy", bot=fresh?"healthy":"stale", botHeartbeat=heartbeat.ToString() }); });
        group.MapGet("/settings", async (IBotSettingsService settings) => Results.Ok(await settings.GetAsync()));
        group.MapPut("/settings", UpdateSettingsAsync);
    }

    private static async Task<IResult> UpdateSettingsAsync(SettingsRequest request, HttpContext http, NightfallDbContext db, IHostEnvironment env)
    {
        if (!ValidCsrf(http)) return Results.BadRequest(new { detail = "Invalid CSRF token." });
        if (request.MinPlayers < 3 || request.MaxPlayers > 50 || request.MinPlayers > request.MaxPlayers) return Results.BadRequest(new { detail = "Player limits must be between 3 and 50 and minimum cannot exceed maximum." });
        if (request.MaintenanceMessage.Length > 4096 || request.HelpMessage.Length > 4096 || request.WelcomeMessage.Length > 4096) return Results.BadRequest(new { detail = "Messages cannot exceed 4096 characters." });
        if (request.EnabledCommands.Except(BotSettingsDefaults.Commands).Any()) return Results.BadRequest(new { detail = "Unknown command." });
        if (!string.IsNullOrWhiteSpace(request.MiniAppBaseUrl) && (!Uri.TryCreate(request.MiniAppBaseUrl, UriKind.Absolute, out var uri) || (!env.IsDevelopment() && uri.Scheme != "https"))) return Results.BadRequest(new { detail = "Mini-app URL must be an absolute HTTPS URL." });
        var row=await db.BotSettings.SingleOrDefaultAsync(x=>x.Id==1) ?? new BotSettingsRecord();
        if (row.Version != request.Version) return Results.Conflict(new { detail="Settings were changed by another session. Reload and try again." });
        var before=JsonSerializer.Serialize(BotSettingsService.ToSnapshot(row));
        row.MinPlayers=request.MinPlayers; row.MaxPlayers=request.MaxPlayers; row.MaintenanceMode=request.MaintenanceMode; row.MaintenanceMessage=request.MaintenanceMessage; row.EnabledCommandsJson=JsonSerializer.Serialize(request.EnabledCommands.Distinct()); row.SoloTestEnabled=request.SoloTestEnabled; row.MiniAppBaseUrl=request.MiniAppBaseUrl.Trim(); row.HelpMessage=request.HelpMessage; row.WelcomeMessage=request.WelcomeMessage; row.Version++; row.UpdatedAt=DateTime.UtcNow;
        if (db.Entry(row).State == EntityState.Detached) db.BotSettings.Add(row);
        db.OperationalEvents.Add(new() { Category="AdminAudit", Message="Bot settings updated", IsAdminAudit=true, TargetType="Settings", TargetId="1", MetadataJson=JsonSerializer.Serialize(new { before, after=BotSettingsService.ToSnapshot(row), actor=http.User.Identity?.Name }) });
        await db.SaveChangesAsync(); return Results.Ok(BotSettingsService.ToSnapshot(row));
    }

    private static async Task<long> CountActiveAsync(IConnectionMultiplexer redis) => (await ActiveKeysAsync(redis)).LongLength;
    private static async Task<List<Nightfall.Domain.GameState>> LoadActiveAsync(IConnectionMultiplexer redis, IGameSessionStore sessions) { var result=new List<Nightfall.Domain.GameState>(); foreach(var key in await ActiveKeysAsync(redis)) if(Guid.TryParse(key.ToString().Split(':').Last(),out var id)&&await sessions.GetAsync(id) is { } game) result.Add(game); return result; }
    private static async Task<RedisKey[]> ActiveKeysAsync(IConnectionMultiplexer redis) { var endpoint=redis.GetEndPoints().FirstOrDefault(); if(endpoint is null)return []; var server=redis.GetServer(endpoint); return await server.KeysAsync(pattern:"nightfall:game:*").ToArrayAsync(); }
}

public sealed record LoginRequest(string Username, string Password);
public sealed record CancelRequest(string Reason);
public sealed record SettingsRequest(int Version,int MinPlayers,int MaxPlayers,bool MaintenanceMode,string MaintenanceMessage,string[] EnabledCommands,bool SoloTestEnabled,string MiniAppBaseUrl,string HelpMessage,string WelcomeMessage);
public sealed record AdminGameView(Guid Id, long TelegramChatId, DateTime CreatedAt, DateTime EndedAt, Nightfall.Domain.WinCondition Result, string Status, string? CancellationReason, IReadOnlyList<AdminGamePlayerView> Players);
public sealed record AdminGamePlayerView(Guid PlayerId, long? TelegramUserId, string TelegramUsername, Nightfall.Domain.Role? Role, bool SurvivedToEnd, int? GodfatherRank);
