namespace BuilderPlatform.Domain.Entities;

public class Artifact
{
    public Guid           Id          { get; set; } = Guid.NewGuid();
    public Guid           ProductId   { get; set; }
    public string         Type        { get; set; } = string.Empty;
    public string         Title       { get; set; } = string.Empty;
    public string         Content     { get; set; } = string.Empty;
    public int            Version     { get; set; } = 1;
    public ArtifactStatus Status      { get; set; } = ArtifactStatus.Draft;
    public DateTime       GeneratedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}

public enum ArtifactStatus
{
    Draft,
    Approved,
    Superseded,
}
