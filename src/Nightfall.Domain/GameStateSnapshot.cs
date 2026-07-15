namespace Nightfall.Domain;

/// <summary>
/// Full-fidelity snapshot of a GameState, including in-flight state (pending night actions/votes,
/// detective's investigation history, doctor cooldown) that isn't otherwise observable from the
/// public API. Exists so Infrastructure can persist/rehydrate a live game (e.g. to Redis) without
/// Domain taking on a framework or serialization dependency itself — GameStateSnapshot and its
/// nested records use only BCL types and are plain data, System.Text.Json-serializable as-is.
/// </summary>
public sealed record GameStateSnapshot(
    Guid GameId,
    DateTimeOffset CreatedAt,
    GamePhase CurrentPhase,
    int NightNumber,
    GameConfig Config,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<NightActionSnapshot> PendingNightActions,
    IReadOnlyDictionary<Guid, Guid?> PendingVotes,
    IReadOnlyCollection<Guid> InvestigatedTargets,
    int? LastDoctorSelfHealNight);

public sealed record PlayerSnapshot(Guid Id, string TelegramUsername, Role? Role, bool IsAlive, int? GodfatherRank);

public sealed record NightActionSnapshot(Guid ActorId, Guid TargetId, NightActionType ActionType);
