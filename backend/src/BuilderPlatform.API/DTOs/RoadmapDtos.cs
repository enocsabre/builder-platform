namespace BuilderPlatform.API.DTOs;

public record RoadmapMilestoneDto(
    string       Id,
    string       Title,
    string       Phase,
    string       Priority,
    string       Category,
    string       Why,
    string       Unlocks,
    List<string> RequiredModules
);

public record RoadmapDependencyDto(
    string From,
    string To,
    string Reason
);

public record StrategicRoadmapDto(
    string                   ProductId,
    string                   Industry,
    string                   IndustryLabel,
    int                      CompletionScore,
    int                      TotalCheckpoints,
    int                      CompletedCheckpoints,
    string                   GrowthNarrative,
    string                   NextFocusTitle,
    string                   NextFocusWhy,
    List<RoadmapMilestoneDto> Milestones,
    List<RoadmapDependencyDto> Dependencies,
    DateTime                 GeneratedAt
);
