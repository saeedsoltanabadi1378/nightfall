using Nightfall.Infrastructure.Sessions;

namespace Nightfall.Api.Games;

public sealed class DiscussionDeadlineService(
    IGameSessionStore sessions,
    IGameNotifier notifier,
    GameMutationLock mutationLock,
    ILogger<DiscussionDeadlineService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var gameId in await sessions.ListActiveIdsAsync())
                {
                    using var held = await mutationLock.EnterAsync(gameId, stoppingToken);
                    var game = await sessions.GetAsync(gameId);
                    if (game is null || game.CurrentPhase != Nightfall.Domain.GamePhase.Day) continue;
                    bool hadDiscussion = game.Discussion is not null;
                    game.EnsureDiscussionStarted(DateTimeOffset.UtcNow);
                    bool advanced = game.AdvanceExpiredDiscussion(DateTimeOffset.UtcNow);
                    if (!hadDiscussion || advanced)
                    {
                        await sessions.SaveAsync(game);
                        await notifier.NotifyGameUpdatedAsync(game);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Failed to advance discussion deadlines."); }
        }
    }
}
