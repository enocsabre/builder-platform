namespace BuilderPlatform.API.DTOs;

public record ArtifactSummaryDto(
    Guid     Id,
    string   Type,
    string   Title,
    int      Version,
    string   Status,
    DateTime GeneratedAt
);

public record ArtifactDto(
    Guid     Id,
    string   Type,
    string   Title,
    string   Content,
    int      Version,
    string   Status,
    DateTime GeneratedAt
);

public record ApproveArtifactRequest(string? Note);
