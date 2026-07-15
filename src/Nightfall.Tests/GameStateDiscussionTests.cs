using Nightfall.Domain;

namespace Nightfall.Tests;

public sealed class GameStateDiscussionTests
{
    [Fact]
    public void ResolveNight_StartsSixtySecondTurnInRosterOrder()
    {
        var (game, players) = TestGameFactory.CreateNightZeroGame(5);
        var before = DateTimeOffset.UtcNow;

        game.ResolveNight();

        Assert.Equal(GamePhase.Day, game.CurrentPhase);
        Assert.NotNull(game.Discussion);
        Assert.Equal(players[0].Id, game.Discussion.ActivePlayerId);
        Assert.Equal(DiscussionSegmentType.Speaker, game.Discussion.SegmentType);
        Assert.InRange(game.Discussion.Deadline, before.AddSeconds(59), before.AddSeconds(61));
    }

    [Fact]
    public void AcceptedChallenge_GetsFortySeconds_ThenAdvancesToNextSpeaker()
    {
        var (game, players) = TestGameFactory.CreateNightZeroGame(5);
        game.ResolveNight();
        var now = DateTimeOffset.UtcNow;

        game.RequestChallenge(players[1].Id, now);
        game.AcceptChallenge(players[0].Id, players[1].Id, now);

        Assert.Equal(DiscussionSegmentType.Challenge, game.Discussion!.SegmentType);
        Assert.Equal(players[1].Id, game.Discussion.ActivePlayerId);
        Assert.Equal(players[0].Id, game.Discussion.OriginalSpeakerId);
        Assert.Equal(now.AddSeconds(40), game.Discussion.Deadline);

        game.FinishDiscussionSegment(players[1].Id, now.AddSeconds(1));
        Assert.Equal(players[1].Id, game.Discussion!.ActivePlayerId);
        Assert.Equal(DiscussionSegmentType.Speaker, game.Discussion.SegmentType);
    }

    [Fact]
    public void FinishingEverySpeaker_AutomaticallyStartsVoting()
    {
        var (game, players) = TestGameFactory.CreateNightZeroGame(5);
        game.ResolveNight();
        var now = DateTimeOffset.UtcNow;

        foreach (var player in players)
            game.FinishDiscussionSegment(player.Id, now);

        Assert.Equal(GamePhase.Voting, game.CurrentPhase);
        Assert.Null(game.Discussion);
    }

    [Fact]
    public void Snapshot_RoundTripsActiveChallengeAndRequests()
    {
        var (game, players) = TestGameFactory.CreateNightZeroGame(5);
        game.ResolveNight();
        game.RequestChallenge(players[1].Id, DateTimeOffset.UtcNow);

        var restored = GameState.FromSnapshot(game.ToSnapshot());

        Assert.NotNull(restored.Discussion);
        Assert.Equal(game.Discussion!.ActivePlayerId, restored.Discussion.ActivePlayerId);
        Assert.Contains(players[1].Id, restored.Discussion.PendingChallengeRequests);
    }
}
