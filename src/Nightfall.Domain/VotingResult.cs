namespace Nightfall.Domain;

public sealed record VotingResult(
    Guid? Eliminated,
    bool WasTie,
    IReadOnlyList<Guid> TiedPlayers,
    Guid? PromotedGodfatherId);
