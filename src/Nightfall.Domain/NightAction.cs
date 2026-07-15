namespace Nightfall.Domain;

internal sealed record NightAction(Guid ActorId, Guid TargetId, NightActionType ActionType);
