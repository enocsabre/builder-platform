namespace BuilderPlatform.Domain.Entities;

public class Product
{
    public Guid          Id           { get; set; } = Guid.NewGuid();
    public string        Name         { get; set; } = string.Empty;
    public string        Prompt       { get; set; } = string.Empty;
    public ProductStatus Status       { get; set; } = ProductStatus.Draft;
    public string?       PreviewUrl          { get; set; }
    public string        PreviewStatus       { get; set; } = "stopped";
    public int?          PreviewPort         { get; set; }
    public DateTime?     PreviewLastStartedAt { get; set; }
    public string?       PreviewError        { get; set; }
    public bool          IsProcessing   { get; set; } = false;
    public string        RuntimePhase   { get; set; } = "idle";
    public string?       ProjectPath    { get; set; }
    public string        ScaffoldStatus { get; set; } = "none";
    public string        RuntimeHealth  { get; set; } = "healthy"; // healthy | degraded | broken | recovering
    public DateTime      CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime      UpdatedAt      { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage>   Messages        { get; set; } = [];
    public ICollection<ActivityEvent> ActivityEvents  { get; set; } = [];
    public ICollection<Approval>      Approvals       { get; set; } = [];
    public ICollection<ProductMemory> Memory          { get; set; } = [];
    public ICollection<Artifact>      Artifacts       { get; set; } = [];
    public ICollection<ScaffoldEntry>  ScaffoldEntries  { get; set; } = [];
    public ICollection<ScaffoldChange> ScaffoldChanges  { get; set; } = [];
    public ICollection<ProductModule>  Modules          { get; set; } = [];
    public ICollection<FileRevision>   FileRevisions    { get; set; } = [];
    public ICollection<ValidationRun>  ValidationRuns   { get; set; } = [];

    // ── Deploy ────────────────────────────────────────────────────────────────
    public string    DeployStatus            { get; set; } = "not_deployed";
    public DateTime? DeployedAt              { get; set; }
    public string?   DeployUrl               { get; set; }
    public string?   DeployLogs              { get; set; }
    public DateTime? LastSuccessfulDeployAt  { get; set; }
    public string?   DeployCommitHash        { get; set; }
    public string?   DeployBranch            { get; set; }

    public ICollection<DeployRun> DeployRuns { get; set; } = [];
}

public enum ProductStatus
{
    Draft,
    Discovering,
    Architecting,
    Planning,
    Building,
    Reviewing,
    Stable,
    Error,
}
