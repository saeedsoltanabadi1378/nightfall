using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nightfall.Api.Auth;
using Nightfall.Api.Errors;
using Nightfall.Api.Games;
using Nightfall.Api.Hubs;
using Nightfall.Infrastructure;
using Nightfall.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

// Registers JwtTokenService/JwtOptions among everything else — see AddNightfallInfrastructure's
// own doc comment for why Jwt is bundled in there rather than registered separately here.
builder.Services.AddNightfallInfrastructure(builder.Configuration);

builder.Services.AddOptions<TelegramAuthOptions>()
    .Bind(builder.Configuration.GetSection(TelegramAuthOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken), $"Missing required configuration: {TelegramAuthOptions.SectionName}:BotToken")
    .ValidateOnStart();
builder.Services.AddSingleton<TelegramInitDataValidator>();

builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<IGameNotifier, SignalRGameNotifier>();
builder.Services.AddSignalR();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
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
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
