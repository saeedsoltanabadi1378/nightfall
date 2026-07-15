using Microsoft.Extensions.Options;
using Nightfall.Bot;
using Nightfall.Infrastructure;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

// Bot only actually uses JwtTokenService + the chat/roster indexes out of everything this
// registers — see AddNightfallInfrastructure's own doc comment for why it's still the single
// shared entry point rather than a separate, narrower registration.
builder.Services.AddNightfallInfrastructure(builder.Configuration);

builder.Services.AddOptions<TelegramBotOptions>()
    .Bind(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken), $"Missing required configuration: {TelegramBotOptions.SectionName}:BotToken")
    .ValidateOnStart();

builder.Services.AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.NightfallApiBaseUrl), $"Missing required configuration: {BotOptions.SectionName}:NightfallApiBaseUrl")
    .ValidateOnStart();

builder.Services.AddHttpClient("telegram-bot-api");
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new TelegramBotClient(options.BotToken, httpClientFactory.CreateClient("telegram-bot-api"));
});

builder.Services.AddHttpClient<INightfallApiClient, NightfallApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    client.BaseAddress = new Uri(options.NightfallApiBaseUrl);
});

builder.Services.AddSingleton<IBotMessenger, TelegramBotMessenger>();
builder.Services.AddSingleton<CommandDispatcher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
