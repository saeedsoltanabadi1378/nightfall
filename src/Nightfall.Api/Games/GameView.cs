using Nightfall.Domain;

namespace Nightfall.Api.Games;

/// <summary>Per-viewer projection of a GameState: only reveals a player's own role plus roles of
/// dead players / everyone once the game has ended. Never reveals other living players' roles or
/// another player's investigation results.</summary>
public sealed record GameView(
    Guid GameId,
    GamePhase Phase,
    int NightNumber,
    IReadOnlyList<PlayerView> Players,
    Guid YourPlayerId,
    Role? YourRole,
    bool YouAreAlive,
    DetectiveResultView? YourLastInvestigationResult,
    EliminationView? LastNightElimination,
    EliminationView? LastVotingElimination,
    WinCondition WinCondition,
    bool YouAreController,
    bool RequiredNightActionsComplete,
    IReadOnlyList<VoteView> Votes,
    DiscussionView? Discussion)
{
    public static GameView For(GameState game, Guid viewerId)
    {
        var viewer = game.GetPlayer(viewerId);
        bool revealAll = game.CurrentPhase == GamePhase.Ended;

        var players = game.Players
            .Select(p => new PlayerView(
                p.Id,
                p.TelegramUsername,
                p.IsAlive,
                RevealedRole: (revealAll || !p.IsAlive || p.Id == viewerId) ? p.Role : null))
            .ToList();

        DetectiveResultView? investigation = null;
        if (viewer?.Role == Role.Detective &&
            game.LastNightResult is { DetectiveTarget: { } targetId, DetectiveResultIsMafiaAligned: { } isMafia })
        {
            investigation = new DetectiveResultView(targetId, isMafia);
        }

        EliminationView? nightElimination = game.LastNightResult is null
            ? null
            : new EliminationView(game.LastNightResult.Eliminated, game.LastNightResult.TargetWasSaved);

        EliminationView? votingElimination = game.LastVotingResult is null
            ? null
            : new EliminationView(game.LastVotingResult.Eliminated, WasSaved: false, game.LastVotingResult.WasTie, game.LastVotingResult.TiedPlayers);

        var votes = game.CurrentPhase == GamePhase.Voting
            ? game.ToSnapshot().PendingVotes.Select(vote => new VoteView(vote.Key, vote.Value)).ToList()
            : [];

        return new GameView(
            game.GameId,
            game.CurrentPhase,
            game.NightNumber,
            players,
            viewerId,
            viewer?.Role,
            viewer?.IsAlive ?? false,
            investigation,
            nightElimination,
            votingElimination,
            game.CheckWinCondition(),
            game.Players.FirstOrDefault()?.Id == viewerId,
            game.AreRequiredNightActionsComplete(),
            votes,
            DiscussionView.For(game, viewerId));
    }
}

public sealed record PlayerView(Guid PlayerId, string TelegramUsername, bool IsAlive, Role? RevealedRole);

public sealed record DetectiveResultView(Guid TargetPlayerId, bool IsMafiaAligned);

public sealed record EliminationView(Guid? EliminatedPlayerId, bool WasSaved, bool WasTie = false, IReadOnlyList<Guid>? TiedPlayers = null);

public sealed record VoteView(Guid VoterPlayerId, Guid? TargetPlayerId);

public sealed record DiscussionView(
    DiscussionSegmentType SegmentType,
    Guid ActivePlayerId,
    Guid OriginalSpeakerId,
    DateTimeOffset Deadline,
    IReadOnlyList<Guid> PendingChallengerIds,
    bool YourChallengeIsPending,
    bool YouCanRequestChallenge,
    bool YouCanFinish)
{
    public static DiscussionView? For(GameState game, Guid viewerId)
    {
        if (game.Discussion is not { } discussion) return null;
        bool isSpeaker = discussion.SegmentType == DiscussionSegmentType.Speaker && discussion.ActivePlayerId == viewerId;
        bool pending = discussion.PendingChallengeRequests.Contains(viewerId);
        var viewer = game.GetPlayer(viewerId);
        return new DiscussionView(
            discussion.SegmentType, discussion.ActivePlayerId, discussion.OriginalSpeakerId, discussion.Deadline,
            isSpeaker ? discussion.PendingChallengeRequests.ToList() : [], pending,
            discussion.SegmentType == DiscussionSegmentType.Speaker && viewer?.IsAlive == true && viewerId != discussion.ActivePlayerId && !pending,
            viewerId == discussion.ActivePlayerId);
    }
}
