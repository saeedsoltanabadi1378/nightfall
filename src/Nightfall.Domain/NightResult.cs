namespace Nightfall.Domain;

public sealed record NightResult(
    int NightNumber,
    Guid? Eliminated,
    bool TargetWasSaved,
    Guid? DetectiveTarget,
    bool? DetectiveResultIsMafiaAligned,
    Guid? PromotedGodfatherId);
