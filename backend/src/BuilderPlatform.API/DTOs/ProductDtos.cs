namespace BuilderPlatform.API.DTOs;

public record CreateProductRequest(string Name, string Prompt);

public record UpdateProductStatusRequest(string Status);

public record UpdatePreviewUrlRequest(string? PreviewUrl);

public record ProductSummaryDto(
    Guid      Id,
    string    Name,
    string    Status,
    string?   PreviewUrl,
    string    PreviewStatus,
    int?      PreviewPort,
    bool      IsProcessing,
    string    RuntimePhase,
    string?   ProjectPath,
    string    ScaffoldStatus,
    string    RuntimeHealth,
    DateTime  CreatedAt,
    DateTime  UpdatedAt
);

public record ProductDetailDto(
    Guid                            Id,
    string                          Name,
    string                          Prompt,
    string                          Status,
    string?                         PreviewUrl,
    string                          PreviewStatus,
    int?                            PreviewPort,
    DateTime?                       PreviewLastStartedAt,
    string?                         PreviewError,
    bool                            IsProcessing,
    string                          RuntimePhase,
    string?                         ProjectPath,
    string                          ScaffoldStatus,
    string                          RuntimeHealth,
    DateTime                        CreatedAt,
    DateTime                        UpdatedAt,
    IEnumerable<MessageDto>         Messages,
    IEnumerable<ActivityDto>        Activity,
    IEnumerable<ApprovalDto>        Approvals,
    IEnumerable<MemoryDto>          Memory,
    IEnumerable<ArtifactSummaryDto> Artifacts,
    IEnumerable<ScaffoldEntryDto>   ScaffoldEntries,
    IEnumerable<ScaffoldChangeDto>  ScaffoldChanges,
    IEnumerable<ProductModuleDto>   Modules,
    IEnumerable<FileRevisionDto>    FileRevisions,
    IEnumerable<ValidationRunSummaryDto> ValidationRuns
);

public record ScaffoldEntryDto(
    Guid     Id,
    string   RelativePath,
    string   EntryType,
    string?  Language,
    int      SortOrder
);

public record ScaffoldChangeDto(
    Guid     Id,
    string   ChangeType,
    string   TargetPath,
    string   ModuleLabel,
    string   Layer,
    DateTime CreatedAt
);

public record ProductModuleDto(
    Guid     Id,
    string   ModuleName,
    string   EntityName,
    string   RoutePath,
    string   ControllerName,
    string   Layer,
    string   Source,
    bool     IsActive,
    DateTime DetectedAt
);

public record MessageDto(
    Guid     Id,
    string   Role,
    string   Content,
    string?  DetectedIntent,
    double?  Confidence,
    DateTime CreatedAt
);

public record ActivityDto(
    Guid     Id,
    string   EventType,
    string   Title,
    string?  Details,
    Guid?    ArtifactId,
    DateTime CreatedAt
);

public record ApprovalDto(
    Guid      Id,
    string    Title,
    string    Description,
    string    Status,
    string?   ResolutionNote,
    Guid?     ArtifactId,
    DateTime  CreatedAt,
    DateTime? ResolvedAt
);

public record MemoryDto(
    string Key,
    string Value,
    DateTime CreatedAt
);

public record FileRevisionDto(
    Guid     Id,
    string   RelativePath,
    string   PatchType,
    string   Reason,
    bool     HasDiff,
    DateTime CreatedAt
);

public record ManagedFileDto(
    string  RelativePath,
    string  DisplayName,
    string  FileType,
    bool    Exists,
    string? LastModified,
    bool    IsEditable,
    int     RevisionCount
);

public record ValidationRunSummaryDto(
    Guid      Id,
    string    Status,
    DateTime  StartedAt,
    DateTime? FinishedAt,
    int       AutofixAttempts,
    int       GatesPassed,
    int       GatesFailed
);

public record GateResultDto(
    string  Gate,
    string  Category,
    bool    Passed,
    bool    Skipped,
    string  Message,
    string? Detail
);

public record ValidationRunDetailDto(
    Guid                        Id,
    string                      Status,
    DateTime                    StartedAt,
    DateTime?                   FinishedAt,
    string?                     Logs,
    string?                     Errors,
    int                         AutofixAttempts,
    int                         GatesPassed,
    int                         GatesFailed,
    IEnumerable<GateResultDto>  GateResults
);

public record SendMessageRequest(string Content);

public record ResolveApprovalRequest(bool Approved, string? Note);
