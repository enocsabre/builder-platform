namespace BuilderPlatform.API.DTOs;

public record IntelligenceGapDto(
    string Module,
    string Reason,
    string Priority,
    string Category
);

public record IntelligenceConnectionDto(
    string From,
    string To,
    string Label,
    bool   Detected,
    string Impact
);

public record IntelligenceSuggestionDto(
    string Title,
    string Context,
    string Impact,
    string Category
);

public record ProactiveInsightDto(
    string Type,
    string Severity,
    string Title,
    string Detail,
    string Action
);

public record IntelligenceReportDto(
    string                             ProductId,
    string                             Industry,
    string                             IndustryLabel,
    int                                ModuleCount,
    string                             EvolutionStage,
    string                             EvolutionStageLabel,
    string                             EvolutionNextMilestone,
    List<IntelligenceGapDto>           Gaps,
    List<IntelligenceConnectionDto>    Connections,
    List<IntelligenceSuggestionDto>    Suggestions,
    string                             Narrative,
    DateTime                           AnalyzedAt,
    // Sprint 39
    string                             HealthScore,
    string                             HealthScoreLabel,
    int                                HealthScoreNumeric,
    int                                CriticalCount,
    List<ProactiveInsightDto>          TopInsights
);
