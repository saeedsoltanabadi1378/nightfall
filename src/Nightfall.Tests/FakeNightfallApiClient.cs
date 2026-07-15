using Nightfall.Bot;
using Nightfall.Domain;
using Nightfall.Infrastructure.Auth;

namespace Nightfall.Tests;

/// <summary>Scriptable stand-in for the real Nightfall.Api HTTP surface, so CommandDispatcher's
/// message-formatting/orchestration logic can be tested without an actual running Api.</summary>
internal sealed class FakeNightfallApiClient : INightfallApiClient
{
    public List<string> Calls { get; } = new();

    public Guid CreatedGameId { get; set; } = Guid.NewGuid();
    public Dictionary<long, GameViewDto> ViewsByTelegramUserId { get; } = new();
    public NightResult NightResultToReturn { get; set; } = new(1, null, false, null, null, null);
    public VotingResult VotingResultToReturn { get; set; } = new(null, false, Array.Empty<Guid>(), null);
    public Exception? ThrowOnNextCall { get; set; }

    private void MaybeThrow()
    {
        if (ThrowOnNextCall is { } ex)
        {
            ThrowOnNextCall = null;
            throw ex;
        }
    }

    public Task<Guid> CreateGameAsync(long telegramChatId, TelegramIdentity actor)
    {
        Calls.Add($"CreateGame({telegramChatId})");
        MaybeThrow();
        return Task.FromResult(CreatedGameId);
    }

    public Task JoinGameAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add($"Join({actor.Id})");
        MaybeThrow();
        return Task.CompletedTask;
    }

    public Task StartGameAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add("StartGame");
        MaybeThrow();
        return Task.CompletedTask;
    }

    public Task<GameViewDto> GetGameAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add($"GetGame({actor.Id})");
        MaybeThrow();
        if (!ViewsByTelegramUserId.TryGetValue(actor.Id, out var view))
            throw new NightfallApiException("no view configured for this actor in the test");
        return Task.FromResult(view);
    }

    public Task<Guid?> GetActiveGameForChatAsync(long telegramChatId, TelegramIdentity actor)
    {
        Calls.Add($"GetActiveGameForChat({telegramChatId})");
        return Task.FromResult<Guid?>(CreatedGameId);
    }

    public Task SubmitNightActionAsync(Guid gameId, TelegramIdentity actor, Guid targetPlayerId, NightActionType actionType)
    {
        Calls.Add($"SubmitNightAction({actor.Id},{actionType})");
        MaybeThrow();
        return Task.CompletedTask;
    }

    public Task<NightResult> ResolveNightAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add("ResolveNight");
        MaybeThrow();
        return Task.FromResult(NightResultToReturn);
    }

    public Task SubmitVoteAsync(Guid gameId, TelegramIdentity actor, Guid? targetPlayerId)
    {
        Calls.Add($"SubmitVote({actor.Id})");
        MaybeThrow();
        return Task.CompletedTask;
    }

    public Task<VotingResult> ResolveVotingAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add("ResolveVoting");
        MaybeThrow();
        return Task.FromResult(VotingResultToReturn);
    }

    public Task StartVotingAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add("StartVoting");
        MaybeThrow();
        return Task.CompletedTask;
    }

    public Task StartNightAsync(Guid gameId, TelegramIdentity actor)
    {
        Calls.Add("StartNight");
        MaybeThrow();
        return Task.CompletedTask;
    }
}
