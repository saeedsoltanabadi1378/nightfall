using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nightfall.Bot;

public sealed class TelegramBotMessenger : IBotMessenger
{
    private readonly ITelegramBotClient _client;

    public TelegramBotMessenger(ITelegramBotClient client)
    {
        _client = client;
    }

    public async Task SendTextAsync(long chatId, string text) =>
        await _client.SendMessage(chatId, text);

    public async Task SendWithMiniAppButtonAsync(long chatId, string text, string? miniAppUrl)
    {
        InlineKeyboardMarkup? markup = miniAppUrl is null
            ? null
            : new InlineKeyboardMarkup(InlineKeyboardButton.WithWebApp("Open Nightfall", new WebAppInfo(miniAppUrl)));

        await _client.SendMessage(chatId, text, replyMarkup: markup);
    }
}
