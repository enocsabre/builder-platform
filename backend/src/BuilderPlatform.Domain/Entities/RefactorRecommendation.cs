namespace BuilderPlatform.Domain.Entities;

public class RefactorRecommendation
{
    public Guid    Id        { get; set; } = Guid.NewGuid();
    public Guid    ProductId { get; set; }

    // "duplicate_module" | "redundant_name" | "ugly_route" | "contradictory_relation" | "missing_connection" | "duplicate_widget"
    public string  Type      { get; set; } = string.Empty;
    public string  Title     { get; set; } = string.Empty;

    // "low" | "medium" | "high"
    public string  Severity  { get; set; } = "medium";
    public string  Reason    { get; set; } = string.Empty;
    public string  Impact    { get; set; } = string.Empty;
    public string  Risk      { get; set; } = string.Empty;

    // "pending" | "accepted" | "rejected" | "applied" | "failed"
    public string  Status    { get; set; } = "pending";
    public string? Note      { get; set; }
    public Guid?   ArtifactId { get; set; }

    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ExecutedAt    { get; set; }
    public string?   ExecutionError { get; set; }

    public Product  Product  { get; set; } = null!;
}
