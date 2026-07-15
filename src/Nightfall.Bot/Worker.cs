using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Nightfall.Bot;

public sealed class Worker : BackgroundService, IUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly CommandDispatcher _dispatcher;
    private readonly ILogger<Worker> _logger;

    public Worker(ITelegramBotClient botClient, CommandDispatcher dispatcher, ILogger<Worker> logger)
    {
        _botClient = botClient;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message },
            DropPendingUpdates = true
        };

        _logger.LogInformation("Nightfall bot starting long-poll receive loop.");

        // ReceiveAsync only returns on cancellation or an unrecoverable error (recoverable
        // per-update errors go through HandleErrorAsync and the loop continues).
        await _botClient.ReceiveAsync(this, receiverOptions, stoppingToken);
    }

    public Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { } message)
        {
            return _dispatcher.HandleMessageAsync(message);
        }
        return Task.CompletedTask;
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error from {Source}", source);
        return Task.CompletedTask;
    }
}
