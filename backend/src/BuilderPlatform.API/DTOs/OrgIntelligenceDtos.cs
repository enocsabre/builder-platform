namespace BuilderPlatform.API.DTOs;

public record OwnershipGapDto(
    string Area,
    string Description,
    string Risk,
    string Severity,
    string SuggestedOwner
);

public record HumanBottleneckDto(
    string Title,
    string Description,
    string Concentration,
    string CollapseRisk,
    string Severity
);

public record RoleSuggestionDto(
    string RoleTitle,
    string Responsibilities,
    string Priority,
    string BusinessCase
);

public record DelegationOpportunityDto(
    string Title,
    string CurrentState,
    string DelegationPath,
    string Impact
);

public record OrgReportDto(
    string                         ProductId,
    string                         Industry,
    string                         IndustryLabel,
    int                            OrgMaturityScore,
    string                         OrgMaturityTier,
    string                         OrgMaturityLabel,
    string                         OrgNarrative,
    string                         TopConcernTitle,
    string                         TopConcernDescription,
    List<OwnershipGapDto>          OwnershipGaps,
    List<HumanBottleneckDto>       HumanBottlenecks,
    List<RoleSuggestionDto>        RoleSuggestions,
    List<DelegationOpportunityDto> TopDelegationOpportunities,
    DateTime                       AnalyzedAt
);
