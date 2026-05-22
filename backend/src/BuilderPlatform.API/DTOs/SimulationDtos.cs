namespace BuilderPlatform.API.DTOs;

public record StartSimulationRequest(string Scenario);

public record SimulationStatusDto(
    bool    IsRunning,
    string? Scenario,
    int     OpsGenerated,
    Guid?   RunId,
    DateTime? StartedAt
);
