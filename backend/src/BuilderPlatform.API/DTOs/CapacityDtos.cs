namespace BuilderPlatform.API.DTOs;

public record SaturationPointDto(
    string Title,
    string Description,
    string Severity,
    string CollapseScenario,
    string AutomationFix,
    int    ScalingRisk
);

public record ManualOperationDto(
    string Title,
    string Description,
    string HumanCost,
    string ImpactOnGrowth,
    string AutomationPath
);

public record AutomationOpportunityDto(
    string Title,
    string OperationalValue,
    string Impact,
    string Urgency,
    string Unlocks
);

public record CapacityReportDto(
    string                          ProductId,
    string                          Industry,
    string                          IndustryLabel,
    int                             CapacityScore,
    string                          CapacityTier,
    string                          CapacityTierLabel,
    string                          ScalingNarrative,
    string                          TopRiskTitle,
    string                          TopRiskDescription,
    List<SaturationPointDto>        SaturationPoints,
    List<ManualOperationDto>        ManualOperations,
    List<AutomationOpportunityDto>  TopAutomationOpportunities,
    DateTime                        AnalyzedAt
);
