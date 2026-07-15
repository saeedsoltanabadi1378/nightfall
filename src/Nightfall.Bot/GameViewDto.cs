using Nightfall.Domain;

namespace Nightfall.Bot;

/// <summary>
/// Mirrors Nightfall.Api.Games.GameView's JSON shape. Kept as a separate client-side DTO rather
/// than referencing the Api project directly — the Bot only ever talks to the Api over HTTP, same
/// as any other client, so it shouldn't depend on the Api's internal presentation types.
/// </summary>
public sealed record GameViewDto(
    Guid GameId,
    GamePhase Phase,
    int NightNumber,
    IReadOnlyList<PlayerViewDto> Players,
    Guid YourPlayerId,
    Role? YourRole,
    bool YouAreAlive,
    DetectiveResultViewDto? YourLastInvestigationResult,
    EliminationViewDto? LastNightElimination,
    EliminationViewDto? LastVotingElimination,
    WinCondition WinCondition,
    bool YouAreController = false,
    bool RequiredNightActionsComplete = false);

public sealed record PlayerViewDto(Guid PlayerId, string TelegramUsername, bool IsAlive, Role? RevealedRole);

public sealed record DetectiveResultViewDto(Guid TargetPlayerId, bool IsMafiaAligned);

public sealed record EliminationViewDto(Guid? EliminatedPlayerId, bool WasSaved, bool WasTie = false, IReadOnlyList<Guid>? TiedPlayers = null);

public sealed record CreateGameResponseDto(Guid GameId);
