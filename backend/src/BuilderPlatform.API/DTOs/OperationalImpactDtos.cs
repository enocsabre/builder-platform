namespace BuilderPlatform.API.DTOs;

public record OperationalBottleneckDto(
    string Title,
    string Description,
    string Severity,
    string ImpactArea,
    string Resolution,
    string Risk,
    int    ImpactScore
);

public record WorkflowStatusDto(
    string       Name,
    bool         IsCritical,
    string       Phase,
    int          Coverage,
    List<string> PresentSteps,
    List<string> MissingSteps,
    string       BusinessImpact
);

public record ImpactSuggestionDto(
    string Title,
    string OperationalValue,
    string ImpactLevel,
    string Urgency
);

public record OperationalReportDto(
    string                      ProductId,
    string                      Industry,
    string                      IndustryLabel,
    int                         OperationalScore,
    string                      OperationalTier,
    string                      OperationalTierLabel,
    string                      OperationalNarrative,
    string                      TopBottleneckTitle,
    string                      TopBottleneckResolution,
    List<OperationalBottleneckDto> Bottlenecks,
    List<WorkflowStatusDto>     Workflows,
    List<ImpactSuggestionDto>   TopImpactSuggestions,
    DateTime                    AnalyzedAt
);
