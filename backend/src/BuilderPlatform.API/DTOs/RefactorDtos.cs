namespace BuilderPlatform.API.DTOs;

public record RefactorRecommendationDto(
    Guid      Id,
    string    Type,
    string    Title,
    string    Severity,
    string    Reason,
    string    Impact,
    string    Risk,
    string    Status,
    string?   Note,
    Guid?     ArtifactId,
    DateTime  CreatedAt,
    DateTime? ResolvedAt,
    DateTime? ExecutedAt,
    string?   ExecutionError
);

public record ResolveRefactorRequest(bool Accepted, string? Note);
