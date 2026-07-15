using System.Net;
using System.Net.Http.Json;
using Nightfall.Api.Games;
using Nightfall.Domain;

namespace Nightfall.Tests;

public class GameFlowIntegrationTests : IClassFixture<NightfallApiFactory>, IAsyncLifetime
{
    private readonly NightfallApiFactory _factory;

    public GameFlowIntegrationTests(NightfallApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureDatabaseCreatedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record TestPlayer(long TelegramUserId, string Username, HttpClient Client);

    private async Task<List<TestPlayer>> CreateAuthenticatedPlayersAsync(int count, long seed)
    {
        var players = new List<TestPlayer>();
        for (int i = 0; i < count; i++)
        {
            long telegramUserId = seed * 1000 + i;
            string username = $"player{seed}_{i}";
            var client = await AuthTestHelper.CreateAuthenticatedClientAsync(_factory, telegramUserId, username);
            players.Add(new TestPlayer(telegramUserId, username, client));
        }
        return players;
    }

    [Fact]
    public async Task FullLobbyToFirstNightCycle_WorksEndToEnd()
    {
        var players = await CreateAuthenticatedPlayersAsync(5, seed: 1);
        var creator = players[0];

        var createResponse = await creator.Client.PostAsJsonAsync("/api/games", new CreateGameRequest(TelegramChatId: 42001));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>();
        Guid gameId = created!.GameId;

        foreach (var player in players.Skip(1))
        {
            var joinResponse = await player.Client.PostAsync($"/api/games/{gameId}/players", null);
            joinResponse.EnsureSuccessStatusCode();
        }

        var startResponse = await creator.Client.PostAsync($"/api/games/{gameId}/start", null);
        startResponse.EnsureSuccessStatusCode();

        // Each player should see their own role, and NOT see other living players' roles.
        var views = new List<GameView>();
        foreach (var player in players)
        {
            var view = await player.Client.GetFromJsonAsync<GameView>($"/api/games/{gameId}");
            Assert.NotNull(view);
            Assert.NotNull(view!.YourRole);
            views.Add(view);

            foreach (var otherPlayerView in view.Players.Where(p => p.PlayerId != view.YourPlayerId))
            {
                Assert.True(otherPlayerView.IsAlive);
                Assert.Null(otherPlayerView.RevealedRole); // hidden: alive, not you, game not ended
            }
        }
        Assert.Equal(GamePhase.NightZero, views[0].Phase);

        var godfather = players[FindIndexByRole(views, Role.Godfather)];
        var detective = players[FindIndexByRole(views, Role.Detective)];
        var doctor = players[FindIndexByRole(views, Role.Doctor)];
        var villagerIndex = views.FindIndex(v => v.YourRole == Role.Villager);
        var villager = players[villagerIndex];
        var villagerId = views[villagerIndex].YourPlayerId;

        var killResponse = await godfather.Client.PostAsJsonAsync($"/api/games/{gameId}/night-actions",
            new SubmitNightActionRequest(villagerId, NightActionType.Kill));
        killResponse.EnsureSuccessStatusCode();

        var investigateResponse = await detective.Client.PostAsJsonAsync($"/api/games/{gameId}/night-actions",
            new SubmitNightActionRequest(villagerId, NightActionType.Investigate));
        investigateResponse.EnsureSuccessStatusCode();

        var doctorSelfId = views[players.IndexOf(doctor)].YourPlayerId;
        var healResponse = await doctor.Client.PostAsJsonAsync($"/api/games/{gameId}/night-actions",
            new SubmitNightActionRequest(doctorSelfId, NightActionType.Heal));
        healResponse.EnsureSuccessStatusCode();

        var resolveNightResponse = await creator.Client.PostAsync($"/api/games/{gameId}/resolve-night", null);
        resolveNightResponse.EnsureSuccessStatusCode();
        var nightResult = await resolveNightResponse.Content.ReadFromJsonAsync<NightResult>();
        Assert.Equal(villagerId, nightResult!.Eliminated);

        // The dead villager's role should now be visible to everyone (dead players are revealed).
        var postNightView = await godfather.Client.GetFromJsonAsync<GameView>($"/api/games/{gameId}");
        var deadVillagerView = postNightView!.Players.Single(p => p.PlayerId == villagerId);
        Assert.False(deadVillagerView.IsAlive);
        Assert.Equal(Role.Villager, deadVillagerView.RevealedRole);
        Assert.Equal(GamePhase.Day, postNightView.Phase);

        // The detective should see their investigation result; nobody else should.
        var detectiveView = await detective.Client.GetFromJsonAsync<GameView>($"/api/games/{gameId}");
        Assert.NotNull(detectiveView!.YourLastInvestigationResult);
        Assert.Equal(villagerId, detectiveView.YourLastInvestigationResult!.TargetPlayerId);
        Assert.False(detectiveView.YourLastInvestigationResult.IsMafiaAligned);

        var villagerView = await villager.Client.GetFromJsonAsync<GameView>($"/api/games/{gameId}");
        Assert.Null(villagerView!.YourLastInvestigationResult);

        // Day -> Voting -> vote out the (living) doctor -> Results.
        var startVotingResponse = await creator.Client.PostAsync($"/api/games/{gameId}/start-voting", null);
        startVotingResponse.EnsureSuccessStatusCode();

        var doctorTargetId = views[players.IndexOf(doctor)].YourPlayerId;
        foreach (var voter in players.Where(p => p != doctor && p != villager))
        {
            var voteResponse = await voter.Client.PostAsJsonAsync($"/api/games/{gameId}/votes", new SubmitVoteRequest(doctorTargetId));
            voteResponse.EnsureSuccessStatusCode();
        }

        var resolveVotingResponse = await creator.Client.PostAsync($"/api/games/{gameId}/resolve-voting", null);
        resolveVotingResponse.EnsureSuccessStatusCode();
        var votingResult = await resolveVotingResponse.Content.ReadFromJsonAsync<VotingResult>();
        Assert.Equal(doctorTargetId, votingResult!.Eliminated);
    }

    [Fact]
    public async Task GetGame_UnknownGameId_Returns404()
    {
        var players = await CreateAuthenticatedPlayersAsync(1, seed: 2);

        var response = await players[0].Client.GetAsync($"/api/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveGameForChat_ResolvesTheGameJustCreated_AndIsClearedWhenTheGameEnds()
    {
        var players = await CreateAuthenticatedPlayersAsync(5, seed: 5);
        var creator = players[0];
        const long chatId = 42004;

        var created = await (await creator.Client.PostAsJsonAsync("/api/games", new CreateGameRequest(chatId)))
            .Content.ReadFromJsonAsync<CreateGameResponse>();

        var byChatResponse = await creator.Client.GetAsync($"/api/games/by-chat/{chatId}");
        byChatResponse.EnsureSuccessStatusCode();
        var byChat = await byChatResponse.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.Equal(created!.GameId, byChat!.GameId);

        // 5 players, mafiaCount == 1 -> voting out the sole Godfather ends the game immediately.
        foreach (var player in players.Skip(1))
        {
            (await player.Client.PostAsync($"/api/games/{created.GameId}/players", null)).EnsureSuccessStatusCode();
        }
        (await creator.Client.PostAsync($"/api/games/{created.GameId}/start", null)).EnsureSuccessStatusCode();
        (await creator.Client.PostAsync($"/api/games/{created.GameId}/resolve-night", null)).EnsureSuccessStatusCode();
        (await creator.Client.PostAsync($"/api/games/{created.GameId}/start-voting", null)).EnsureSuccessStatusCode();

        // Find whichever authenticated player IS the Godfather by checking each one's own view.
        TestPlayer? godfather = null;
        Guid godfatherPlayerId = default;
        foreach (var player in players)
        {
            var v = await player.Client.GetFromJsonAsync<GameView>($"/api/games/{created.GameId}");
            if (v!.YourRole == Role.Godfather)
            {
                godfather = player;
                godfatherPlayerId = v.YourPlayerId;
            }
        }
        Assert.NotNull(godfather);

        foreach (var voter in players.Where(p => p != godfather))
        {
            (await voter.Client.PostAsJsonAsync($"/api/games/{created.GameId}/votes", new SubmitVoteRequest(godfatherPlayerId)))
                .EnsureSuccessStatusCode();
        }
        var resolveResponse = await creator.Client.PostAsync($"/api/games/{created.GameId}/resolve-voting", null);
        resolveResponse.EnsureSuccessStatusCode();

        var afterEndResponse = await creator.Client.GetAsync($"/api/games/by-chat/{chatId}");
        Assert.Equal(HttpStatusCode.NotFound, afterEndResponse.StatusCode);
    }

    [Fact]
    public async Task SubmitNightAction_OutsideNightPhase_Returns400()
    {
        var players = await CreateAuthenticatedPlayersAsync(5, seed: 3);
        var creator = players[0];
        var created = await (await creator.Client.PostAsJsonAsync("/api/games", new CreateGameRequest(42002)))
            .Content.ReadFromJsonAsync<CreateGameResponse>();

        foreach (var player in players.Skip(1))
        {
            (await player.Client.PostAsync($"/api/games/{created!.GameId}/players", null)).EnsureSuccessStatusCode();
        }
        // Game is still in Lobby -> night actions should be rejected as a GameException (400), not a 500.
        var response = await creator.Client.PostAsJsonAsync($"/api/games/{created!.GameId}/night-actions",
            new SubmitNightActionRequest(Guid.NewGuid(), NightActionType.Kill));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_Return401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/games", new CreateGameRequest(1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VoiceToken_MainChannel_Succeeds_MafiaChannel_ForbiddenToNonMafia()
    {
        var players = await CreateAuthenticatedPlayersAsync(5, seed: 4);
        var creator = players[0];
        var created = await (await creator.Client.PostAsJsonAsync("/api/games", new CreateGameRequest(42003)))
            .Content.ReadFromJsonAsync<CreateGameResponse>();
        foreach (var player in players.Skip(1))
        {
            (await player.Client.PostAsync($"/api/games/{created!.GameId}/players", null)).EnsureSuccessStatusCode();
        }
        (await creator.Client.PostAsync($"/api/games/{created!.GameId}/start", null)).EnsureSuccessStatusCode();

        var view = await creator.Client.GetFromJsonAsync<GameView>($"/api/games/{created!.GameId}");
        var mainTokenResponse = await creator.Client.GetAsync($"/api/games/{created.GameId}/voice-token?channel=main");
        mainTokenResponse.EnsureSuccessStatusCode();
        var mainToken = await mainTokenResponse.Content.ReadFromJsonAsync<VoiceTokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(mainToken!.Token));

        if (view!.YourRole is not (Role.Mafia or Role.Godfather))
        {
            var mafiaTokenResponse = await creator.Client.GetAsync($"/api/games/{created.GameId}/voice-token?channel=mafia");
            Assert.Equal(HttpStatusCode.Forbidden, mafiaTokenResponse.StatusCode);
        }
    }

    private static int FindIndexByRole(List<GameView> views, Role role)
    {
        int index = views.FindIndex(v => v.YourRole == role);
        Assert.True(index >= 0, $"No player found with role {role}.");
        return index;
    }
}
