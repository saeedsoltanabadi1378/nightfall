using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nightfall.Api.Auth;
using Nightfall.Api.Admin;
using Nightfall.Api.Errors;
using Nightfall.Api.Games;
using Nightfall.Api.Hubs;
using Nightfall.Infrastructure;
using Nightfall.Infrastructure.Auth;
using Nightfall.Infrastructure.History;

var builder = WebApplication.CreateBuilder(args);

// Registers JwtTokenService/JwtOptions among everything else — see AddNightfallInfrastructure's
// own doc comment for why Jwt is bundled in there rather than registered separately here.
builder.Services.AddNightfallInfrastructure(builder.Configuration);

builder.Services.AddOptions<TelegramAuthOptions>()
    .Bind(builder.Configuration.GetSection(TelegramAuthOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken), $"Missing required configuration: {TelegramAuthOptions.SectionName}:BotToken")
    .ValidateOnStart();
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddOptions<AdminOptions>().Bind(builder.Configuration.GetSection(AdminOptions.SectionName));

builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<GameMutationLock>();
builder.Services.AddHostedService<DiscussionDeadlineService>();
builder.Services.AddSingleton<IGameNotifier, SignalRGameNotifier>();
builder.Services.AddSignalR();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "nightfall-admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
// Bind JwtBearerOptions lazily from IOptions<JwtOptions> (resolved at host-startup time, after all
// configuration sources — including test overrides — are fully composed) rather than reading
// builder.Configuration directly here, which would only see whatever sources exist *so far*.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
    {
        var jwt = jwtOptionsAccessor.Value;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Browsers can't set an Authorization header on the WebSocket upgrade request SignalR
        // uses, so the client sends the JWT as an `access_token` query param instead for hub paths.
        bearerOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options => options.AddPolicy("AdminOnly", p => p.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme).RequireRole("Admin")));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("admin-login", o => { o.PermitLimit = 5; o.Window = TimeSpan.FromMinutes(1); o.QueueLimit = 0; }));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    });
});

builder.Services.AddExceptionHandler<NightfallExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapGameEndpoints();
app.MapHub<GameHub>("/hubs/game");

// Applies pending EF Core migrations before serving traffic. IsRelational() guards this so it's a
// no-op under the InMemory provider (WebApplicationFactory-based tests, which set up their schema
// via EnsureCreated instead — Migrate() isn't supported there and would throw).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NightfallDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

// `--migrate-only`: apply migrations then exit, without starting the server — lets an operator
// run schema migrations as an explicit, separate step ahead of a coordinated rollout.
if (args.Contains("--migrate-only"))
{
    return;
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
