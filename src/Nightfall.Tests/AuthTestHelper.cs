using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nightfall.Api.Auth;

namespace Nightfall.Tests;

internal static class AuthTestHelper
{
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(NightfallApiFactory factory, long telegramUserId, string username)
    {
        var client = factory.CreateClient();
        string initData = TelegramInitDataFixture.Sign(NightfallApiFactory.TestBotToken, telegramUserId, username);

        var response = await client.PostAsJsonAsync("/api/auth/telegram", new TelegramAuthRequest(initData));
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<TelegramAuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        return client;
    }
}
