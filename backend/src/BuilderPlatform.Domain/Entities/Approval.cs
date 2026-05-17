namespace BuilderPlatform.Domain.Entities;

public class Approval
{
    public Guid           Id             { get; set; } = Guid.NewGuid();
    public Guid           ProductId      { get; set; }
    public string         Title          { get; set; } = string.Empty;
    public string         Description    { get; set; } = string.Empty;
    public ApprovalStatus Status         { get; set; } = ApprovalStatus.Pending;
    public string?        ResolutionNote { get; set; }
    public Guid?          ArtifactId     { get; set; }
    public DateTime       CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime?      ResolvedAt     { get; set; }

    public Product   Product  { get; set; } = null!;
    public Artifact? Artifact { get; set; }
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
}
