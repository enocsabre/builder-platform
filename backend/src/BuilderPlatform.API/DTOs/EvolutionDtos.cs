namespace BuilderPlatform.API.DTOs;

public record EvolutionModuleDto(string Name, string Route, string Layer, DateTime AddedAt);
public record EvolutionRelationDto(string From, string To, string RelationType, string Reason, DateTime DetectedAt);
public record EvolutionDecisionDto(string Summary, DateTime MadeAt);

public record EvolutionContextDto(
    List<EvolutionModuleDto>   Modules,
    List<EvolutionRelationDto> Relations,
    List<EvolutionDecisionDto> Decisions,
    List<string>               FeatureHistory
);
